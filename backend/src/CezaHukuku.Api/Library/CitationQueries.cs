using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Text;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Library;

/// <summary>Madde metnindeki atıf: fıkra/bent içindeki konumu ve bağlandığı madde.</summary>
/// <param name="ItemId">Atıf bentteyse bendin kimliği; fıkranın kendi metnindeyse null.</param>
public sealed record CitationSpan(Guid ParagraphId, Guid? ItemId, int Start, int Length, LinkedArticleRef Target);

internal static class CitationQueries
{
    /// <summary>
    /// Gösterilen sürümdeki atıflar, hedefleri çözülmüş olarak. Hedef kütüphanede yoksa (kanun girilmemiş) ya da
    /// kullanıcıya kapalıysa atıf düz metin kalır.
    /// </summary>
    public static async Task<List<CitationSpan>> ForVersionAsync(
        AppDbContext db, Guid versionId, Law sourceLaw, LawOutline sourceOutline, bool includePrivate)
    {
        var rows = await db.ArticleCitations.AsNoTracking().Where(c => c.SourceVersionId == versionId).ToListAsync();
        if (rows.Count == 0)
            return [];

        // Aynı kanuna atıflar okunan kanunun ağacından (görünür maddeler) çözülür.
        var sameLaw = sourceOutline.ReadingOrder.ToDictionary(a => a.Article.Slug, a => a.Article);

        // Başka kanunlar numara ya da adla: kütüphane küçük, kanunlar bir kez yüklenir.
        var laws = await db.Laws.AsNoTracking().ToListAsync();
        Law? LawFor(ArticleCitation c) => c.TargetKind switch
        {
            CitationTargetKind.LawNumber => laws.FirstOrDefault(l => l.Number == c.TargetLaw),
            CitationTargetKind.LawName => laws.FirstOrDefault(l => TurkishText.Fold(l.Name) == c.TargetLaw),
            _ => null,
        };
        var external = rows.Where(c => c.TargetKind != CitationTargetKind.SameLaw)
            .Select(c => (Citation: c, Law: LawFor(c)))
            .Where(x => x.Law is not null)
            .ToList();
        var externalLawIds = external.Select(x => x.Law!.Id).Distinct().ToList();
        var externalSlugs = external.Select(x => x.Citation.TargetArticleSlug).Distinct().ToList();
        var candidates = await db.Articles.AsNoTracking()
            .Where(a => externalLawIds.Contains(a.LawId) && externalSlugs.Contains(a.Slug))
            .ToListAsync();
        var visible = await VisibleArticles.FilterAsync(db, candidates, includePrivate);
        var externalTargets = visible.Values.ToDictionary(v => (v.Law.Id, v.Article.Slug));

        var spans = new List<CitationSpan>();
        foreach (var c in rows)
        {
            LinkedArticleRef? target = null;
            if (c.TargetKind == CitationTargetKind.SameLaw)
            {
                if (sameLaw.TryGetValue(c.TargetArticleSlug, out var article))
                    target = VisibleArticles.ToRef(article, sourceLaw);
            }
            else if (LawFor(c) is { } law && externalTargets.TryGetValue((law.Id, c.TargetArticleSlug), out var hit))
            {
                target = VisibleArticles.ToRef(hit.Article, hit.Law);
            }

            if (target is not null)
                spans.Add(new CitationSpan(c.ParagraphId, c.ItemId, c.Start, c.Length, target));
        }
        return spans;
    }

    /// <summary>Bu maddeye yürürlükteki metninde atıf yapan maddeler (kendisi hariç), kanun ve madde sırasıyla.</summary>
    public static async Task<List<LinkedArticleRef>> CitedByAsync(AppDbContext db, Article article, Law law, bool includePrivate)
    {
        var foldedName = TurkishText.Fold(law.Name);
        var sourceIds = await (
            from c in db.ArticleCitations
            join v in db.ArticleVersions on c.SourceVersionId equals v.Id
            join source in db.Articles on c.SourceArticleId equals source.Id
            where c.TargetArticleSlug == article.Slug
                  && v.ValidTo == null
                  && c.SourceArticleId != article.Id
                  && ((c.TargetKind == CitationTargetKind.SameLaw && source.LawId == law.Id)
                      || (c.TargetKind == CitationTargetKind.LawNumber && c.TargetLaw == law.Number)
                      || (c.TargetKind == CitationTargetKind.LawName && c.TargetLaw == foldedName))
            select c.SourceArticleId
        ).Distinct().ToListAsync();
        if (sourceIds.Count == 0)
            return [];

        var sources = await db.Articles.AsNoTracking().Where(a => sourceIds.Contains(a.Id)).ToListAsync();
        var visible = await VisibleArticles.FilterAsync(db, sources, includePrivate);
        return
        [
            .. visible.Values
                .OrderBy(v => v.Law.Id == law.Id ? 0 : 1).ThenBy(v => v.Law.SortOrder).ThenBy(v => v.Article.SortOrder)
                .Select(v => VisibleArticles.ToRef(v.Article, v.Law)),
        ];
    }
}
