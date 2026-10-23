using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Library;

/// <summary>Bağlantının karşı ucundaki madde (okuma ekranındaki adresiyle).</summary>
public sealed record LinkedArticleRef(
    Guid ArticleId,
    string LawSlug,
    string LawLabel,
    string LawName,
    string ArticleSlug,
    string Number,
    string Title);

/// <param name="Incoming">Bağlantı karşı maddeden bu maddeye kurulmuş (ör. 82'ye bakarken 81 → 82 "ilgili").</param>
/// <param name="Article">Karşı madde; kütüphanede olmayan hedefte null, <paramref name="ExternalLabel"/> dolu.</param>
/// <param name="TargetVersion">Eski kanun karşılığında hedef maddenin metni (yürürlükteki ya da son sürümü).</param>
public sealed record ArticleLinkDto(
    Guid Id,
    ArticleLinkKind Kind,
    bool Incoming,
    LinkedArticleRef? Article,
    string? ExternalLabel,
    string? Note,
    ArticleVersionDetail? TargetVersion);

internal static class ArticleLinkQueries
{
    /// <summary>
    /// Maddenin bağlantıları: kendi kurduğu (giden) ve ona kurulmuş (gelen). Karşı madde kullanıcıya kapalıysa
    /// bağlantı hiç gösterilmez; karşı maddenin geçerli görünürlüğü kendi kanununun ağacına göre hesaplanır.
    /// </summary>
    public static async Task<List<ArticleLinkDto>> VisibleLinksAsync(AppDbContext db, Guid articleId, bool includePrivate)
    {
        var links = await db.ArticleLinks.AsNoTracking()
            .Where(l => l.SourceArticleId == articleId || l.TargetArticleId == articleId)
            .OrderBy(l => l.Kind).ThenBy(l => l.SortOrder).ThenBy(l => l.CreatedAt)
            .ToListAsync();
        if (links.Count == 0)
            return [];

        var otherIds = links
            .Select(l => l.SourceArticleId == articleId ? l.TargetArticleId : l.SourceArticleId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => otherIds.Contains(a.Id)).ToListAsync();
        var lawIds = articles.Select(a => a.LawId).Distinct().ToList();
        var laws = await db.Laws.AsNoTracking().Where(l => lawIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
        var nodesByLaw = (await db.LawNodes.AsNoTracking().Where(n => lawIds.Contains(n.LawId)).ToListAsync())
            .ToLookup(n => n.LawId);

        var visibleArticles = new Dictionary<Guid, (Article Article, Law Law)>();
        foreach (var article in articles)
        {
            // Silinmiş kanunun maddesi gösterilmez (sorgu filtresi kanunu getirmez).
            if (!laws.TryGetValue(article.LawId, out var law))
                continue;
            var outline = LawOutline.Build(law, nodesByLaw[law.Id], [], includePrivate: true);
            if (includePrivate || outline.EffectiveVisibilityOf(article) == Visibility.Public)
                visibleArticles[article.Id] = (article, law);
        }

        // Eski kanun karşılığında hedef maddenin metni de gösterilir.
        var counterpartIds = links
            .Where(l => l.Kind == ArticleLinkKind.EskiKanunKarsiligi && l.SourceArticleId == articleId)
            .Select(l => l.TargetArticleId)
            .OfType<Guid>()
            .Where(visibleArticles.ContainsKey)
            .ToList();
        var counterpartVersions = (await db.ArticleVersions.AsNoTracking()
                .Where(v => counterpartIds.Contains(v.ArticleId))
                .ToListAsync())
            .ToLookup(v => v.ArticleId);

        var result = new List<ArticleLinkDto>();
        var seenRelated = new HashSet<Guid>();
        foreach (var link in links)
        {
            var incoming = link.SourceArticleId != articleId;
            var otherId = incoming ? link.SourceArticleId : link.TargetArticleId;

            LinkedArticleRef? other = null;
            if (otherId is { } id)
            {
                if (!visibleArticles.TryGetValue(id, out var target))
                    continue;
                // İki madde birbirine ayrı ayrı "ilgili" bağlanmışsa bir kez gösterilir.
                if (link.Kind == ArticleLinkKind.Ilgili && !seenRelated.Add(id))
                    continue;
                other = new LinkedArticleRef(id, target.Law.Slug, target.Law.ShortName ?? target.Law.Number, target.Law.Name,
                    target.Article.Slug, target.Article.Number, target.Article.Title);
            }
            else if (incoming)
            {
                continue;
            }

            var targetVersion = !incoming && link.Kind == ArticleLinkKind.EskiKanunKarsiligi && otherId is { } targetId
                ? ArticleVersion.Select(counterpartVersions[targetId], null)?.ToDetail()
                : null;
            result.Add(new ArticleLinkDto(link.Id, link.Kind, incoming, other, link.ExternalLabel, link.Note, targetVersion));
        }
        return result;
    }
}
