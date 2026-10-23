using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Library;

/// <summary>
/// Başka kanunlardaki maddelerden (bağlantı, atıf hedefleri) kullanıcının görebileceklerini süzer: maddenin geçerli
/// görünürlüğü kendi kanununun ağacına göre hesaplanır. Silinmiş kanunun maddesi elenir.
/// </summary>
internal static class VisibleArticles
{
    public static async Task<Dictionary<Guid, (Article Article, Law Law)>> FilterAsync(
        AppDbContext db, IReadOnlyCollection<Article> articles, bool includePrivate)
    {
        var lawIds = articles.Select(a => a.LawId).Distinct().ToList();
        var laws = await db.Laws.AsNoTracking().Where(l => lawIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
        var nodesByLaw = (await db.LawNodes.AsNoTracking().Where(n => lawIds.Contains(n.LawId)).ToListAsync())
            .ToLookup(n => n.LawId);

        var outlines = new Dictionary<Guid, LawOutline>();
        var visible = new Dictionary<Guid, (Article, Law)>();
        foreach (var article in articles)
        {
            if (!laws.TryGetValue(article.LawId, out var law))
                continue;
            if (!outlines.TryGetValue(law.Id, out var outline))
                outlines[law.Id] = outline = LawOutline.Build(law, nodesByLaw[law.Id], [], includePrivate: true);
            if (includePrivate || outline.EffectiveVisibilityOf(article) == Visibility.Public)
                visible[article.Id] = (article, law);
        }
        return visible;
    }

    public static LinkedArticleRef ToRef(Article article, Law law) =>
        new(article.Id, law.Slug, law.ShortName ?? law.Number, law.Name, article.Slug, article.Number, article.Title);
}
