using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Legislation;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <summary>Maddeler ve madde metninin yürürlük sürümleri.</summary>
internal static class AdminArticleEndpoints
{
    private const int MaxParagraphLength = 20_000;

    public static void MapArticleEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/laws/{lawId:guid}/articles", CreateAsync).WithName("CreateArticle");
        group.MapPut("/articles/{id:guid}", UpdateAsync).WithName("UpdateArticle");
        group.MapDelete("/articles/{id:guid}", DeleteAsync).WithName("DeleteArticle");

        group.MapGet("/article-versions/{id:guid}", GetVersionAsync).WithName("GetArticleVersion");
        group.MapPost("/articles/{articleId:guid}/versions", CreateVersionAsync).WithName("CreateArticleVersion");
        group.MapPut("/article-versions/{id:guid}", UpdateVersionAsync).WithName("UpdateArticleVersion");
        group.MapDelete("/article-versions/{id:guid}", DeleteVersionAsync).WithName("DeleteArticleVersion");

        group.MapPost("/article-text/parse", ParseTextAsync).WithName("ParseArticleText");
    }

    private static async Task<Results<Ok<ArticleVersionDetail>, NotFound>> GetVersionAsync(Guid id, AppDbContext db)
    {
        var version = await db.ArticleVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
        return version is null ? TypedResults.NotFound() : TypedResults.Ok(version.ToDetail());
    }

    /// <summary>
    /// Metni kaydetmeden fıkra ve bentlere ayırır (yazarken önizleme). Dönen fıkralar kimlikleriyle birlikte
    /// sürüm kaydetme isteğine aynen gönderilir.
    /// </summary>
    private static async Task<Ok<List<ParagraphDto>>> ParseTextAsync(ParseArticleTextRequest request, AppDbContext db)
    {
        IReadOnlyList<Paragraph> paragraphs =
        [
            .. ArticleTextParser.Parse(request.Text).Select(p => new Paragraph(
                Guid.CreateVersion7(), p.Number, p.Text,
                [.. p.Items.Select(i => new ParagraphItem(Guid.CreateVersion7(), i.Level, i.Label, i.Text))])),
        ];

        if (request.BaseVersionId is { } baseId
            && await db.ArticleVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == baseId) is { } baseVersion)
        {
            paragraphs = ParagraphIdentity.CarryOver(baseVersion.Paragraphs, paragraphs);
        }

        return TypedResults.Ok(paragraphs.Select(p => p.ToDto()).ToList());
    }

    private static async Task<Results<Created<ArticleSummary>, NotFound, ValidationProblem>> CreateAsync(
        Guid lawId, SaveArticleRequest request, AppDbContext db)
    {
        if (!await db.Laws.AnyAsync(l => l.Id == lawId))
            return TypedResults.NotFound();

        var article = new Article { LawId = lawId, Number = "", Slug = "" };
        if (await ApplyAsync(article, request, db) is { } problem)
            return problem;

        article.SortOrder = request.SortOrder ?? await AdminLawNodeEndpoints.NextSortOrderAsync(db, lawId, article.NodeId);
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/admin/articles/{article.Id}", await ToSummaryAsync(article, db));
    }

    private static async Task<Results<Ok<ArticleSummary>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, SaveArticleRequest request, AppDbContext db)
    {
        var article = await db.Articles.FindAsync(id);
        if (article is null)
            return TypedResults.NotFound();

        var previousNode = article.NodeId;
        if (await ApplyAsync(article, request, db) is { } problem)
            return problem;

        article.SortOrder = request.SortOrder
            ?? (article.NodeId == previousNode
                ? article.SortOrder
                : await AdminLawNodeEndpoints.NextSortOrderAsync(db, article.LawId, article.NodeId));
        await db.SaveChangesAsync();
        return TypedResults.Ok(await ToSummaryAsync(article, db));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, AppDbContext db, TimeProvider clock)
    {
        var article = await db.Articles.FindAsync(id);
        if (article is null)
            return TypedResults.NotFound();

        article.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<ValidationProblem?> ApplyAsync(Article article, SaveArticleRequest request, AppDbContext db)
    {
        var errors = new ValidationErrors()
            .Required("number", request.Number, 50)
            .MaxLength("title", request.Title, 500);

        var number = InputText.CleanRequired(request.Number);
        var slug = Slugs.ForArticle(number);

        if (!errors.HasErrors && await db.Articles.AnyAsync(a => a.LawId == article.LawId && a.Slug == slug && a.Id != article.Id))
            errors.Add("number", "Bu kanunda aynı numaralı bir madde zaten var.");

        if (request.NodeId is { } nodeId && !await db.LawNodes.AnyAsync(n => n.Id == nodeId && n.LawId == article.LawId))
            errors.Add("nodeId", "Düğüm bu kanunda bulunamadı.");

        if (errors.HasErrors)
            return errors.ToProblem();

        article.NodeId = request.NodeId;
        article.Number = number;
        article.Slug = slug;
        article.Title = InputText.CleanRequired(request.Title);
        article.Visibility = request.Visibility;
        return null;
    }

    /// <summary>Geçerli görünürlük üst düğümlere bağlı olduğundan kanunun ağacı üzerinden hesaplanır.</summary>
    private static async Task<ArticleSummary> ToSummaryAsync(Article article, AppDbContext db)
    {
        var law = await db.Laws.AsNoTracking().FirstAsync(l => l.Id == article.LawId);
        var nodes = await db.LawNodes.AsNoTracking().Where(n => n.LawId == article.LawId).ToListAsync();
        var outline = LawOutline.Build(law, nodes, [], includePrivate: true);
        return article.ToSummary(outline.EffectiveVisibilityOf(article));
    }

    private static async Task<Results<Created<ArticleVersionDetail>, NotFound, ValidationProblem>> CreateVersionAsync(
        Guid articleId, SaveArticleVersionRequest request, AppDbContext db)
    {
        if (!await db.Articles.AnyAsync(a => a.Id == articleId))
            return TypedResults.NotFound();

        var others = await db.ArticleVersions.Where(v => v.ArticleId == articleId).ToListAsync();
        var version = new ArticleVersion { ArticleId = articleId };
        var errors = Apply(version, request);

        // Yeni sürüm hâlen yürürlükteyse, o güne kadar yürürlükte olan sürüm bir gün önce kapatılır.
        if (!errors.HasErrors && version.ValidTo is null && others.FirstOrDefault(v => v.ValidTo is null) is { } open)
        {
            if (version.ValidFrom is not { } from)
                errors.Add("validFrom", "Yürürlükte bir sürüm var; yeni sürümün yürürlük başlangıcını girin.");
            else if (open.ValidFrom is { } openFrom && openFrom >= from)
                errors.Add("validFrom", $"Yeni sürüm, yürürlükteki sürümden ({openFrom:dd.MM.yyyy}) sonra başlamalı.");
            else
                open.ValidTo = from.AddDays(-1);
        }

        CheckOverlaps(version, others, errors);
        if (errors.HasErrors)
            return errors.ToProblem();

        db.ArticleVersions.Add(version);
        await CitationIndex.ReplaceForPendingVersionsAsync(db);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/admin/article-versions/{version.Id}", version.ToDetail());
    }

    /// <summary>Sürümü yerinde düzeltir (yazım hatası, eksik tarih). Metin değişikliği için yeni sürüm eklenmeli.</summary>
    private static async Task<Results<Ok<ArticleVersionDetail>, NotFound, ValidationProblem>> UpdateVersionAsync(
        Guid id, SaveArticleVersionRequest request, AppDbContext db)
    {
        var version = await db.ArticleVersions.FindAsync(id);
        if (version is null)
            return TypedResults.NotFound();

        var others = await db.ArticleVersions.AsNoTracking()
            .Where(v => v.ArticleId == version.ArticleId && v.Id != id)
            .ToListAsync();
        var errors = Apply(version, request);
        CheckOverlaps(version, others, errors);
        if (errors.HasErrors)
            return errors.ToProblem();

        await CitationIndex.ReplaceForPendingVersionsAsync(db);
        await db.SaveChangesAsync();
        return TypedResults.Ok(version.ToDetail());
    }

    /// <summary>Yürürlükteki sürüm silinirse, ondan hemen önce biten sürüm yeniden yürürlükte sayılır.</summary>
    private static async Task<Results<NoContent, NotFound>> DeleteVersionAsync(Guid id, AppDbContext db, TimeProvider clock)
    {
        var version = await db.ArticleVersions.FindAsync(id);
        if (version is null)
            return TypedResults.NotFound();

        if (version.ValidTo is null && version.ValidFrom is { } from)
        {
            var previousEnd = from.AddDays(-1);
            var previous = await db.ArticleVersions
                .FirstOrDefaultAsync(v => v.ArticleId == version.ArticleId && v.Id != id && v.ValidTo == previousEnd);
            previous?.ValidTo = null;
        }

        version.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static ValidationErrors Apply(ArticleVersion version, SaveArticleVersionRequest request)
    {
        var errors = new ValidationErrors()
            .MaxLength("amendedBy", request.AmendedBy, 500)
            .MaxLength("gazetteNumber", request.GazetteNumber, 50);

        if (request.ValidFrom is { } from && request.ValidTo is { } to && to < from)
            errors.Add("validTo", "Yürürlük sonu başlangıçtan önce olamaz.");

        var paragraphs = BuildParagraphs(request.Paragraphs ?? [], errors);
        if (paragraphs.Count == 0 && !request.IsRepealed)
            errors.Add("paragraphs", "En az bir fıkra girin (madde mülga ise \"mülga\" olarak işaretleyin).");

        if (errors.HasErrors)
            return errors;

        version.ValidFrom = request.ValidFrom;
        version.ValidTo = request.ValidTo;
        version.AmendedBy = InputText.Clean(request.AmendedBy);
        version.GazetteDate = request.GazetteDate;
        version.GazetteNumber = InputText.Clean(request.GazetteNumber);
        version.IsRepealed = request.IsRepealed;
        version.Paragraphs = paragraphs;
        return errors;
    }

    private static void CheckOverlaps(ArticleVersion version, IEnumerable<ArticleVersion> others, ValidationErrors errors)
    {
        if (errors.HasErrors)
            return;
        if (others.FirstOrDefault(version.Overlaps) is { } overlapping)
        {
            errors.Add("validFrom",
                $"Yürürlük aralığı başka bir sürümle çakışıyor ({Format(overlapping.ValidFrom)} – {Format(overlapping.ValidTo)}).");
        }
    }

    private static string Format(DateOnly? date) => date?.ToString("dd.MM.yyyy") ?? "…";

    private static List<Paragraph> BuildParagraphs(List<ParagraphInput> inputs, ValidationErrors errors)
    {
        var paragraphs = new List<Paragraph>(inputs.Count);
        var ids = new HashSet<Guid>();

        Guid NewOrKept(Guid? id, string field)
        {
            var value = id ?? Guid.CreateVersion7();
            if (!ids.Add(value))
                errors.Add(field, "Aynı kimlik birden fazla fıkrada ya da bentte kullanılmış.");
            return value;
        }

        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            var field = $"paragraphs[{i}]";
            var text = InputText.CleanRequired(input.Text);
            var items = new List<ParagraphItem>();

            foreach (var (item, j) in (input.Items ?? []).Select((item, j) => (item, j)))
            {
                var itemField = $"{field}.items[{j}]";
                var itemText = InputText.CleanRequired(item.Text);
                if (item.Level is < 0 or > 2)
                    errors.Add($"{itemField}.level", "Seviye 0 (bağlayıcı metin), 1 (bent) ya da 2 (alt bent) olmalı.");
                if (itemText.Length == 0)
                    errors.Add($"{itemField}.text", "Bent metni boş olamaz.");
                errors.MaxLength($"{itemField}.text", itemText, MaxParagraphLength).MaxLength($"{itemField}.label", item.Label, 20);
                items.Add(new ParagraphItem(NewOrKept(item.Id, $"{itemField}.id"), item.Level, InputText.Clean(item.Label), itemText));
            }

            if (text.Length == 0 && items.Count == 0)
                errors.Add($"{field}.text", "Fıkra metni boş olamaz.");
            errors.MaxLength($"{field}.text", text, MaxParagraphLength);
            paragraphs.Add(new Paragraph(NewOrKept(input.Id, $"{field}.id"), input.Number, text, items));
        }

        return paragraphs;
    }
}
