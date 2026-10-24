using CezaHukuku.Api.Common;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <param name="Source">Bu kanundaki madde numarası ("81", "Geçici 1").</param>
/// <param name="Target">Eski kanundaki karşılık; birden çoksa virgülle ("448, 449").</param>
public sealed record OldLawLinkRow(string Source, string Target, string? Note);

/// <param name="TargetLawId">Eski kanun kütüphanedeyse: karşılıklar maddelerine bağlanır (metni gösterilir).</param>
/// <param name="TargetLabel">Kütüphanede değilse etiket öneki: "765 s. TCK" → "765 s. TCK m. 448".</param>
/// <param name="DryRun">Yalnız denetle, kaydetme (önizleme).</param>
public sealed record BulkOldLawLinksRequest(Guid? TargetLawId, string? TargetLabel, List<OldLawLinkRow> Rows, bool DryRun);

public enum BulkRowStatus
{
    Created,
    Exists,
    Error,
}

/// <param name="Row">Tablodaki satır (1'den).</param>
public sealed record BulkOldLawLinkResultRow(int Row, string Source, string Target, BulkRowStatus Status, string? Message);

public sealed record BulkOldLawLinksResult(int Created, int Exists, int Errors, List<BulkOldLawLinkResultRow> Rows);

/// <summary>
/// Eski kanun karşılıklarını (TCK–765 karşılaştırma tablosu) toplu girer: Excel'den yapıştırılan "yeni madde | eski
/// madde | not" satırları. Var olan bağlantı tekrar eklenmez; hatalı satır diğerlerini engellemez.
/// </summary>
internal static class AdminBulkLinkEndpoints
{
    private const int MaxRows = 2000;

    public static void MapBulkLinkEndpoints(this RouteGroupBuilder group) =>
        group.MapPost("/laws/{lawId:guid}/old-law-links/bulk", BulkAsync).WithName("BulkOldLawLinks");

    private static async Task<Results<Ok<BulkOldLawLinksResult>, NotFound, ValidationProblem>> BulkAsync(
        Guid lawId, BulkOldLawLinksRequest request, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Laws.AnyAsync(l => l.Id == lawId, ct))
            return TypedResults.NotFound();
        var targetLabel = InputText.Clean(request.TargetLabel);
        if (request.TargetLawId is { } targetLawId)
        {
            if (targetLawId == lawId || !await db.Laws.AnyAsync(l => l.Id == targetLawId, ct))
                return ValidationErrors.Single("targetLawId", "Eski kanunu seçin.");
        }
        else if (targetLabel is null)
        {
            return ValidationErrors.Single("targetLabel", "Eski kanun kütüphanede yoksa etiketini yazın (örn. 765 s. TCK).");
        }
        if (request.Rows is not { Count: > 0 and <= MaxRows })
            return ValidationErrors.Single("rows", $"1 ile {MaxRows} arasında satır yapıştırın.");

        var sources = await db.Articles.Where(a => a.LawId == lawId).ToDictionaryAsync(a => a.Slug, ct);
        var targets = request.TargetLawId is { } id
            ? await db.Articles.Where(a => a.LawId == id).ToDictionaryAsync(a => a.Slug, ct)
            : [];
        var sourceIds = sources.Values.Select(a => a.Id).ToList();
        var existing = await db.ArticleLinks
            .Where(l => l.Kind == ArticleLinkKind.EskiKanunKarsiligi && sourceIds.Contains(l.SourceArticleId))
            .ToListAsync(ct);
        var nextOrder = existing.GroupBy(l => l.SourceArticleId).ToDictionary(g => g.Key, g => g.Max(l => l.SortOrder));

        var results = new List<BulkOldLawLinkResultRow>();
        var added = new List<ArticleLink>();
        foreach (var (row, index) in request.Rows.Select((r, i) => (r, i + 1)))
        {
            var sourceNumber = InputText.CleanRequired(row.Source);
            if (sourceNumber.Length == 0 || !sources.TryGetValue(Slugs.ForArticle(sourceNumber), out var source))
            {
                results.Add(new(index, row.Source, row.Target, BulkRowStatus.Error, $"Bu kanunda “{sourceNumber}” maddesi yok."));
                continue;
            }
            var targetNumbers = (row.Target ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (targetNumbers.Length == 0)
            {
                results.Add(new(index, row.Source, row.Target ?? "", BulkRowStatus.Error, "Eski kanun maddesi boş."));
                continue;
            }
            var note = InputText.Clean(row.Note);
            if (note is { Length: > 500 })
                note = note[..500];

            foreach (var targetNumber in targetNumbers)
            {
                ArticleLink link;
                if (request.TargetLawId is not null)
                {
                    if (!targets.TryGetValue(Slugs.ForArticle(targetNumber), out var target))
                    {
                        results.Add(new(index, sourceNumber, targetNumber, BulkRowStatus.Error, $"Eski kanunda “{targetNumber}” maddesi yok."));
                        continue;
                    }
                    link = new ArticleLink { SourceArticleId = source.Id, TargetArticleId = target.Id };
                }
                else
                {
                    link = new ArticleLink { SourceArticleId = source.Id, ExternalLabel = $"{targetLabel} m. {targetNumber}" };
                }

                var duplicate = existing.Concat(added).Any(l => l.SourceArticleId == link.SourceArticleId
                    && (link.TargetArticleId is not null ? l.TargetArticleId == link.TargetArticleId : l.ExternalLabel == link.ExternalLabel));
                if (duplicate)
                {
                    results.Add(new(index, sourceNumber, targetNumber, BulkRowStatus.Exists, null));
                    continue;
                }

                link.Kind = ArticleLinkKind.EskiKanunKarsiligi;
                link.Note = note;
                link.SortOrder = nextOrder[source.Id] = nextOrder.GetValueOrDefault(source.Id) + 1;
                added.Add(link);
                results.Add(new(index, sourceNumber, targetNumber, BulkRowStatus.Created, null));
            }
        }

        if (!request.DryRun && added.Count > 0)
        {
            db.ArticleLinks.AddRange(added);
            await db.SaveChangesAsync(ct);
        }
        return TypedResults.Ok(new BulkOldLawLinksResult(
            results.Count(r => r.Status == BulkRowStatus.Created),
            results.Count(r => r.Status == BulkRowStatus.Exists),
            results.Count(r => r.Status == BulkRowStatus.Error),
            results));
    }
}
