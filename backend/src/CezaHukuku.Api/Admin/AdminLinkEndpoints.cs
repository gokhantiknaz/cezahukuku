using CezaHukuku.Api.Common;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Legislation;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <param name="TargetArticleId">Kütüphanedeki hedef madde; yoksa <paramref name="ExternalLabel"/> zorunlu.</param>
/// <param name="ExternalLabel">Kütüphanede olmayan hedef: "765 s. TCK m. 448".</param>
public sealed record SaveArticleLinkRequest(ArticleLinkKind Kind, Guid? TargetArticleId, string? ExternalLabel, string? Note);

public sealed record ArticleSearchHit(Guid ArticleId, string LawSlug, string LawLabel, string ArticleSlug, string Number, string Title);

/// <summary>Madde bağlantıları (ilgili madde, eski kanun karşılığı) ve bağlanacak maddeyi bulmak için arama.</summary>
internal static class AdminLinkEndpoints
{
    public static void MapLinkEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/article-search", SearchArticlesAsync).WithName("SearchArticles");
        group.MapPost("/articles/{articleId:guid}/links", CreateAsync).WithName("CreateArticleLink");
        group.MapPut("/article-links/{id:guid}", UpdateAsync).WithName("UpdateArticleLink");
        group.MapDelete("/article-links/{id:guid}", DeleteAsync).WithName("DeleteArticleLink");
        group.MapPost("/citations/rebuild", RebuildCitationsAsync).WithName("RebuildCitations");
    }

    /// <summary>Bütün sürümlerdeki madde atıflarını yeniden tarar (atıf kuralları güncellendiğinde).</summary>
    private static async Task<Ok<int>> RebuildCitationsAsync(AppDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await CitationIndex.RebuildAllAsync(db, cancellationToken));

    /// <summary>
    /// "tck 81", "765 448", "5237/81" gibi göndermede doğrudan o madde; değilse başlık ya da numarada geçen ifade.
    /// </summary>
    private static async Task<Ok<List<ArticleSearchHit>>> SearchArticlesAsync(string? q, AppDbContext db)
    {
        var text = InputText.Clean(q);
        if (text is null)
            return TypedResults.Ok(new List<ArticleSearchHit>());

        var query =
            from article in db.Articles
            join law in db.Laws on article.LawId equals law.Id
            select new { article, law };

        if (ArticleReferenceParser.Parse(text) is { } reference)
        {
            var slug = Slugs.ForArticle(reference.ArticleNumber);
            var laws = await db.Laws.AsNoTracking().ToListAsync();
            var lawIds = laws
                .Where(l => l.Slug == reference.Law || l.Number == reference.Law || Slugs.FromText(l.ShortName) == reference.Law)
                .Select(l => l.Id)
                .ToList();
            var exact = await query.Where(x => lawIds.Contains(x.law.Id) && x.article.Slug == slug)
                .Select(x => new ArticleSearchHit(x.article.Id, x.law.Slug, x.law.ShortName ?? x.law.Number, x.article.Slug,
                    x.article.Number, x.article.Title))
                .ToListAsync();
            if (exact.Count > 0)
                return TypedResults.Ok(exact);
        }

        var pattern = $"%{text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
        var hits = await query
            .Where(x => EF.Functions.ILike(x.article.Title, pattern) || EF.Functions.ILike(x.article.Number, pattern))
            .OrderBy(x => x.law.SortOrder).ThenBy(x => x.article.SortOrder)
            .Take(20)
            .Select(x => new ArticleSearchHit(x.article.Id, x.law.Slug, x.law.ShortName ?? x.law.Number, x.article.Slug,
                x.article.Number, x.article.Title))
            .ToListAsync();
        return TypedResults.Ok(hits);
    }

    private static async Task<Results<Created<Guid>, NotFound, ValidationProblem>> CreateAsync(
        Guid articleId, SaveArticleLinkRequest request, AppDbContext db)
    {
        if (!await db.Articles.AnyAsync(a => a.Id == articleId))
            return TypedResults.NotFound();

        var link = new ArticleLink { SourceArticleId = articleId };
        if (await ApplyAsync(link, request, db) is { } problem)
            return problem;

        link.SortOrder = (await db.ArticleLinks.Where(l => l.SourceArticleId == articleId && l.Kind == link.Kind)
            .MaxAsync(l => (int?)l.SortOrder) ?? 0) + 1;
        db.ArticleLinks.Add(link);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/admin/article-links/{link.Id}", link.Id);
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, SaveArticleLinkRequest request, AppDbContext db)
    {
        var link = await db.ArticleLinks.FindAsync(id);
        if (link is null)
            return TypedResults.NotFound();
        if (await ApplyAsync(link, request, db) is { } problem)
            return problem;

        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, AppDbContext db, TimeProvider clock)
    {
        var link = await db.ArticleLinks.FindAsync(id);
        if (link is null)
            return TypedResults.NotFound();

        link.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<ValidationProblem?> ApplyAsync(ArticleLink link, SaveArticleLinkRequest request, AppDbContext db)
    {
        var errors = new ValidationErrors()
            .MaxLength("externalLabel", request.ExternalLabel, 200)
            .MaxLength("note", request.Note, 500);
        var label = InputText.Clean(request.ExternalLabel);

        if (request.TargetArticleId is { } targetId)
        {
            if (targetId == link.SourceArticleId)
                errors.Add("targetArticleId", "Madde kendisine bağlanamaz.");
            else if (!await db.Articles.AnyAsync(a => a.Id == targetId))
                errors.Add("targetArticleId", "Hedef madde bulunamadı.");
            else if (await db.ArticleLinks.AnyAsync(l => l.Id != link.Id && l.Kind == request.Kind
                         && ((l.SourceArticleId == link.SourceArticleId && l.TargetArticleId == targetId)
                             || (request.Kind == ArticleLinkKind.Ilgili && l.SourceArticleId == targetId && l.TargetArticleId == link.SourceArticleId))))
            {
                errors.Add("targetArticleId", "Bu bağlantı zaten var.");
            }
        }
        else if (label is null)
        {
            errors.Add("externalLabel", "Kütüphaneden bir madde seçin ya da maddenin adını yazın (ör. 765 s. TCK m. 448).");
        }

        if (errors.HasErrors)
            return errors.ToProblem();

        link.Kind = request.Kind;
        link.TargetArticleId = request.TargetArticleId;
        link.ExternalLabel = request.TargetArticleId is null ? label : null;
        link.Note = InputText.Clean(request.Note);
        return null;
    }
}
