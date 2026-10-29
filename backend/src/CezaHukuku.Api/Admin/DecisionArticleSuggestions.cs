using CezaHukuku.Api.Library;
using CezaHukuku.Infrastructure.Content;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Legislation;
using CezaHukuku.Parsing.Text;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <param name="Text">Kararın özeti ve/veya metni; düz metin ya da düzenleyiciden gelen HTML.</param>
/// <param name="DecisionId">Düzenlenen karar: bağlı olduğu maddeler "bağlı" diye işaretlenir.</param>
public sealed record SuggestDecisionArticlesRequest(string? Text, Guid? DecisionId);

/// <param name="Mentions">Metinde kaç kez geçtiği.</param>
/// <param name="Cited">Metinde geçtiği biçimler ("TCK 81/2", "5237 sayılı Kanunun 82 nci maddesi").</param>
/// <param name="Excerpt">İlk geçtiği yerin çevresi.</param>
/// <param name="Linked">Karar bu maddeye zaten bağlı.</param>
public sealed record DecisionArticleSuggestion(LinkedArticle Article, int Mentions, List<string> Cited, string Excerpt, bool Linked);

/// <param name="NotFound">Kütüphanede karşılığı bulunamayan atıflar ("TCK 999", "2918 sayılı Kanunun 48. maddesi").</param>
public sealed record DecisionArticleSuggestions(List<DecisionArticleSuggestion> Items, List<string> NotFound);

/// <summary>
/// Karar metninden madde bağlantısı önerisi: metindeki atıflar (<see cref="DocumentCitationFinder"/>) kütüphanedeki
/// maddelere çözülür; yönetici hangilerini bağlayacağını seçer. Kısaltması tanınmayan atıf ("KDV 18") önerilmez.
/// </summary>
internal static class DecisionArticleSuggestionsQuery
{
    private const int MaxTextLength = 500_000;
    private const int ExcerptRadius = 70;

    public static async Task<DecisionArticleSuggestions> FindAsync(
        AppDbContext db, SuggestDecisionArticlesRequest request, CancellationToken ct)
    {
        var raw = request.Text ?? "";
        if (raw.Length > MaxTextLength)
            raw = raw[..MaxTextLength];
        var text = raw.Contains('<') ? RichText.ToPlainText(RichText.Sanitize(raw)) : raw;
        var found = DocumentCitationFinder.Find(text);
        if (found.Count == 0)
            return new([], []);

        var laws = await CitedLaws.LoadAsync(db, includePrivate: true, ct);
        var cited = found
            .Select(c => (Citation: c, Law: laws.Find(c), Slug: Slugs.ForArticle(c.ArticleNumber)))
            .Where(x => x.Law is not null || x.Citation.Kind != DocumentLawKind.Abbreviation)
            .ToList();
        var lawIds = cited.Where(x => x.Law is not null).Select(x => x.Law!.Id).Distinct().ToList();
        var slugs = cited.Select(x => x.Slug).Distinct().ToList();
        var articles = (await db.Articles.AsNoTracking()
                .Where(a => lawIds.Contains(a.LawId) && slugs.Contains(a.Slug)).ToListAsync(ct))
            .ToDictionary(a => (a.LawId, a.Slug));
        var linked = request.DecisionId is { } decisionId
            ? (await db.ArticleDecisions.Where(l => l.DecisionId == decisionId).Select(l => l.ArticleId).ToListAsync(ct)).ToHashSet()
            : [];

        var items = new List<DecisionArticleSuggestion>();
        var notFound = new List<string>();
        foreach (var group in cited.GroupBy(x => x.Law is not null && articles.TryGetValue((x.Law.Id, x.Slug), out var a) ? a : null))
        {
            if (group.Key is not { } article)
            {
                notFound.AddRange(group.Select(x => Quote(text, x.Citation)));
                continue;
            }
            var law = group.First().Law!;
            var first = group.First().Citation;
            items.Add(new DecisionArticleSuggestion(
                new LinkedArticle(article.Id, law.Slug, law.ShortName ?? law.Number, article.Slug, article.Number, article.Title),
                group.Count(),
                [.. group.Select(x => Quote(text, x.Citation)).Distinct(StringComparer.CurrentCultureIgnoreCase).Take(5)],
                Excerpt(text, first),
                linked.Contains(article.Id)));
        }

        // Çok geçen önce; eşitse kanun ve madde sırasıyla.
        var order = (await db.Laws.AsNoTracking().Where(l => lawIds.Contains(l.Id)).Select(l => new { l.Id, l.SortOrder }).ToListAsync(ct))
            .ToDictionary(l => l.Id, l => l.SortOrder);
        var sortOrder = articles.Values.ToDictionary(a => a.Id, a => (order.GetValueOrDefault(a.LawId), a.SortOrder));
        return new(
            [.. items.OrderByDescending(i => i.Mentions).ThenBy(i => sortOrder[i.Article.ArticleId])],
            [.. notFound.Distinct(StringComparer.CurrentCultureIgnoreCase).Take(20)]);
    }

    private static string Quote(string text, DocumentCitation citation) =>
        string.Join(' ', text.Substring(citation.Start, citation.Length).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Atfın çevresi, kelime sınırında kesilir: "… sanığın eylemi TCK'nın 81. maddesi kapsamında …".</summary>
    private static string Excerpt(string text, DocumentCitation citation)
    {
        var start = Math.Max(0, citation.Start - ExcerptRadius);
        var end = Math.Min(text.Length, citation.Start + citation.Length + ExcerptRadius);
        if (start > 0 && text.IndexOf(' ', start, citation.Start - start) is var space and >= 0)
            start = space + 1;
        if (end < text.Length && text.LastIndexOf(' ', end - 1, end - (citation.Start + citation.Length)) is var last and >= 0)
            end = last;
        var excerpt = string.Join(' ', text[start..end].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return (start > 0 ? "… " : "") + excerpt + (end < text.Length ? " …" : "");
    }
}
