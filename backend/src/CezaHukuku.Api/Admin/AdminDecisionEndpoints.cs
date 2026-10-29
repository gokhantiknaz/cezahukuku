using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Content;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <summary>
/// Mahkeme kararları (içtihat, norm denetimi). Karar bir kez girilir, maddelere ayrıca bağlanır; aynı karar birden çok
/// maddede görünebilir.
/// </summary>
internal static class AdminDecisionEndpoints
{
    private const int MaxHtmlLength = 500_000;

    public static void MapDecisionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/decisions", ListAsync).WithName("ListDecisions");
        group.MapGet("/decisions/{id:guid}", GetAsync).WithName("GetDecision");
        group.MapPost("/decisions", CreateAsync).WithName("CreateDecision");
        group.MapPut("/decisions/{id:guid}", UpdateAsync).WithName("UpdateDecision");
        group.MapDelete("/decisions/{id:guid}", DeleteAsync).WithName("DeleteDecision");
        group.MapPost("/decisions/article-suggestions", SuggestArticlesAsync).WithName("SuggestDecisionArticles");
        group.MapPut("/articles/{articleId:guid}/decisions/{decisionId:guid}", LinkAsync).WithName("LinkDecision");
        group.MapDelete("/articles/{articleId:guid}/decisions/{decisionId:guid}", UnlinkAsync).WithName("UnlinkDecision");
    }

    /// <param name="q">Esas/karar numarası, daire, özet ya da metinde geçen ifade.</param>
    /// <param name="kind">"ictihat" ya da "normDenetimi".</param>
    private static async Task<Ok<AdminDecisionList>> ListAsync(
        string? q, string? kind, int? page, int? pageSize, AppDbContext db)
    {
        var size = Math.Clamp(pageSize ?? 20, 1, 100);
        var skip = (Math.Max(page ?? 1, 1) - 1) * size;
        var query = db.CourtDecisions.AsNoTracking();

        if (Enum.TryParse<DecisionKind>(kind, ignoreCase: true, out var kindFilter))
            query = query.Where(d => d.Kind == kindFilter);
        if (InputText.Clean(q) is { } text)
        {
            var pattern = $"%{text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
            query = query.Where(d =>
                EF.Functions.ILike(d.CaseNumber ?? "", pattern)
                || EF.Functions.ILike(d.DecisionNumber ?? "", pattern)
                || EF.Functions.ILike(d.Chamber ?? "", pattern)
                || EF.Functions.ILike(d.Summary, pattern)
                || EF.Functions.ILike(d.PlainText, pattern));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.DecisionDate).ThenByDescending(d => d.CreatedAt)
            .Skip(skip).Take(size)
            .Select(d => new { Decision = d, Count = db.ArticleDecisions.Count(l => l.DecisionId == d.Id) })
            .ToListAsync();

        return TypedResults.Ok(new AdminDecisionList(
            [.. items.Select(i => new AdminDecisionListItem(i.Decision.ToDto(i.Decision.Visibility), i.Count))], total));
    }

    private static async Task<Results<Ok<AdminDecisionDetail>, NotFound>> GetAsync(Guid id, AppDbContext db)
    {
        var decision = await db.CourtDecisions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        return decision is null ? TypedResults.NotFound() : TypedResults.Ok(await ToDetailAsync(decision, db));
    }

    private static async Task<Results<Created<AdminDecisionDetail>, ValidationProblem>> CreateAsync(
        SaveDecisionRequest request, AppDbContext db)
    {
        var decision = new CourtDecision();
        var errors = Apply(decision, request);

        var articleIds = (request.ArticleIds ?? []).Distinct().ToList();
        if (articleIds.Count > 0 && await db.Articles.CountAsync(a => articleIds.Contains(a.Id)) != articleIds.Count)
            errors.Add("articleIds", "Bağlanacak maddelerden biri bulunamadı.");
        if (errors.HasErrors)
            return errors.ToProblem();

        db.CourtDecisions.Add(decision);
        db.ArticleDecisions.AddRange(articleIds.Select(articleId => new ArticleDecision { ArticleId = articleId, DecisionId = decision.Id }));
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/admin/decisions/{decision.Id}", await ToDetailAsync(decision, db));
    }

    private static async Task<Results<Ok<AdminDecisionDetail>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, SaveDecisionRequest request, AppDbContext db)
    {
        var decision = await db.CourtDecisions.FindAsync(id);
        if (decision is null)
            return TypedResults.NotFound();
        var errors = Apply(decision, request);

        // Güncellemede verilen maddeler bağlantılara eklenir; var olan bağlantılar kaldırılmaz.
        var linked = await db.ArticleDecisions.Where(l => l.DecisionId == id).Select(l => l.ArticleId).ToListAsync();
        var articleIds = (request.ArticleIds ?? []).Distinct().Except(linked).ToList();
        if (articleIds.Count > 0 && await db.Articles.CountAsync(a => articleIds.Contains(a.Id)) != articleIds.Count)
            errors.Add("articleIds", "Bağlanacak maddelerden biri bulunamadı.");
        if (errors.HasErrors)
            return errors.ToProblem();

        db.ArticleDecisions.AddRange(articleIds.Select(articleId => new ArticleDecision { ArticleId = articleId, DecisionId = id }));
        await db.SaveChangesAsync();
        return TypedResults.Ok(await ToDetailAsync(decision, db));
    }

    /// <summary>Karar silinir (soft delete); bağlı olduğu maddelerde artık görünmez.</summary>
    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, AppDbContext db, TimeProvider clock)
    {
        var decision = await db.CourtDecisions.FindAsync(id);
        if (decision is null)
            return TypedResults.NotFound();

        decision.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Ok<DecisionArticleSuggestions>> SuggestArticlesAsync(
        SuggestDecisionArticlesRequest request, AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await DecisionArticleSuggestionsQuery.FindAsync(db, request, ct));

    private static async Task<Results<NoContent, NotFound>> LinkAsync(Guid articleId, Guid decisionId, AppDbContext db)
    {
        if (!await db.Articles.AnyAsync(a => a.Id == articleId) || !await db.CourtDecisions.AnyAsync(d => d.Id == decisionId))
            return TypedResults.NotFound();

        if (!await db.ArticleDecisions.AnyAsync(l => l.ArticleId == articleId && l.DecisionId == decisionId))
        {
            db.ArticleDecisions.Add(new ArticleDecision { ArticleId = articleId, DecisionId = decisionId });
            await db.SaveChangesAsync();
        }
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnlinkAsync(Guid articleId, Guid decisionId, AppDbContext db)
    {
        await db.ArticleDecisions.Where(l => l.ArticleId == articleId && l.DecisionId == decisionId).ExecuteDeleteAsync();
        return TypedResults.NoContent();
    }

    private static ValidationErrors Apply(CourtDecision decision, SaveDecisionRequest request)
    {
        var errors = new ValidationErrors()
            .MaxLength("chamber", request.Chamber, 200)
            .MaxLength("caseNumber", request.CaseNumber, 50)
            .MaxLength("decisionNumber", request.DecisionNumber, 50)
            .MaxLength("gazetteNumber", request.GazetteNumber, 50)
            .MaxLength("summary", request.Summary, 4000)
            .MaxLength("sourceUrl", request.SourceUrl, 1000);

        if (request.Kind == DecisionKind.NormDenetimi && request.Court != Court.AnayasaMahkemesi)
            errors.Add("court", "Norm denetimi kararları Anayasa Mahkemesine aittir.");
        if (request.Kind == DecisionKind.NormDenetimi && request.Outcome is null)
            errors.Add("outcome", "Norm denetimi kararının sonucunu seçin (iptal, kısmen iptal, ret).");

        var sourceUrl = InputText.Clean(request.SourceUrl);
        if (sourceUrl is not null
            && !(Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
        {
            errors.Add("sourceUrl", "Kaynak bağlantısı http:// ya da https:// ile başlamalı.");
        }

        if (request.Html is { Length: > MaxHtmlLength })
            errors.Add("html", "Metin çok uzun.");
        var html = RichText.Sanitize(request.Html);
        var summary = InputText.Clean(request.Summary) ?? "";
        if (summary.Length == 0 && html.Length == 0)
            errors.Add("summary", "Kararın özetini ya da metnini girin.");
        if (InputText.Clean(request.CaseNumber) is null && InputText.Clean(request.DecisionNumber) is null && request.DecisionDate is null)
            errors.Add("decisionNumber", "Kararı tanımlamak için esas no, karar no ya da karar tarihinden en az birini girin.");

        if (errors.HasErrors)
            return errors;

        decision.Court = request.Court;
        decision.Chamber = InputText.Clean(request.Chamber);
        decision.Kind = request.Kind;
        decision.CaseNumber = InputText.Clean(request.CaseNumber);
        decision.DecisionNumber = InputText.Clean(request.DecisionNumber);
        decision.DecisionDate = request.DecisionDate;
        decision.Outcome = request.Kind == DecisionKind.NormDenetimi ? request.Outcome : null;
        decision.GazetteDate = request.GazetteDate;
        decision.GazetteNumber = InputText.Clean(request.GazetteNumber);
        decision.Summary = summary;
        decision.Html = html;
        decision.PlainText = RichText.ToPlainText(html);
        decision.SourceUrl = sourceUrl;
        decision.Visibility = request.Visibility;
        return errors;
    }

    private static async Task<AdminDecisionDetail> ToDetailAsync(CourtDecision decision, AppDbContext db)
    {
        var articles = await (
            from link in db.ArticleDecisions
            join article in db.Articles on link.ArticleId equals article.Id
            join law in db.Laws on article.LawId equals law.Id
            where link.DecisionId == decision.Id
            orderby law.SortOrder, article.SortOrder
            select new LinkedArticle(article.Id, law.Slug, law.ShortName ?? law.Number, article.Slug, article.Number, article.Title)
        ).ToListAsync();
        return new AdminDecisionDetail(decision.ToDto(decision.Visibility), articles);
    }
}
