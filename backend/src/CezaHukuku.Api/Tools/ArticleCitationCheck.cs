using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Decisions;
using CezaHukuku.Parsing.Legislation;
using CezaHukuku.Parsing.Text;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Tools;

/// <summary>
/// Metindeki madde atıflarını kütüphaneye göre denetler: madde var mı, verilen fıkra ve bent var mı, madde mülga mı,
/// değişti mi, Anayasa Mahkemesi iptal etti mi. Olay tarihi verilirse o tarihteki metin esas alınır ve sonradan
/// değişen madde işaretlenir (lehe kanun, TCK m. 7). Kütüphanede olmayan kanunun maddesi denetlenemez (bilgi); kısaltması
/// tanınmayan atıf ("KDV 18", "TL 500") hiç gösterilmez.
/// </summary>
internal static class ArticleCitationCheck
{
    public static async Task<List<CheckedArticleCitation>> CheckAsync(
        AppDbContext db, string text, DateOnly? asOf, bool includePrivate, CancellationToken ct)
    {
        var found = DocumentCitationFinder.Find(text);
        if (found.Count == 0)
            return [];

        var laws = await CitedLaws.LoadAsync(db, includePrivate, ct);
        var cited = found
            .Select(c => (Citation: c, Law: laws.Find(c), Slug: Slugs.ForArticle(c.ArticleNumber)))
            .Where(x => x.Law is not null || x.Citation.Kind != DocumentLawKind.Abbreviation)
            .ToList();
        var lawIds = cited.Where(x => x.Law is not null).Select(x => x.Law!.Id).Distinct().ToList();
        var slugs = cited.Select(x => x.Slug).Distinct().ToList();

        var candidates = await db.Articles.AsNoTracking()
            .Where(a => lawIds.Contains(a.LawId) && slugs.Contains(a.Slug)).ToListAsync(ct);
        var visible = await VisibleArticles.FilterAsync(db, candidates, includePrivate);
        var articles = visible.Values.ToDictionary(v => (v.Law.Id, v.Article.Slug), v => v.Article);
        var articleIds = visible.Keys.ToList();
        var versions = (await db.ArticleVersions.AsNoTracking().Where(v => articleIds.Contains(v.ArticleId)).ToListAsync(ct))
            .ToLookup(v => v.ArticleId);
        var annulments = await AnnulmentsAsync(db, articleIds, includePrivate, ct);
        var lawsWithArticles = (await db.Articles.AsNoTracking()
            .Where(a => lawIds.Contains(a.LawId)).Select(a => a.LawId).Distinct().ToListAsync(ct)).ToHashSet();

        var inspected = new Dictionary<string, (string Label, LinkedArticleRef? Ref, List<CitationIssue> Issues)>();
        var result = new List<CheckedArticleCitation>();
        foreach (var (citation, law, slug) in cited)
        {
            var key = $"{law?.Id.ToString() ?? TurkishText.Fold(citation.LawValue)}|{slug}|{citation.Paragraph}|{citation.Item}";
            if (!inspected.TryGetValue(key, out var entry))
            {
                var article = law is null ? null : articles.GetValueOrDefault((law.Id, slug));
                entry = article is null
                    ? (text.Substring(citation.Start, citation.Length), null, [Unresolved(citation, law, lawsWithArticles)])
                    : (Label(law!, article, citation), VisibleArticles.ToRef(article, law!),
                        Inspect(law!, article, [.. versions[article.Id]], citation, asOf, annulments[article.Id]));
                inspected[key] = entry;
            }
            result.Add(new CheckedArticleCitation(citation.Start, citation.Length, key, entry.Label, entry.Ref,
                citation.Paragraph, citation.Item, entry.Issues.Max(i => i.Severity), entry.Issues));
        }
        return result;
    }

    private static CitationIssue Unresolved(DocumentCitation citation, Law? law, HashSet<Guid> lawsWithArticles)
    {
        if (law is null)
            return new("kanun_yok", CheckSeverity.Info, $"“{citation.LawValue}” kütüphanede yok; denetlenemedi.", null);
        var lawLabel = law.ShortName ?? law.Number;
        return lawsWithArticles.Contains(law.Id)
            ? new("madde_yok", CheckSeverity.Error,
                $"{lawLabel}: {citation.ArticleNumber}. madde kütüphanede bulunamadı; madde numarasını kontrol edin.", $"/{law.Slug}")
            : new("kanun_bos", CheckSeverity.Info, $"{lawLabel} maddeleri kütüphaneye girilmemiş; denetlenemedi.", $"/{law.Slug}");
    }

    private static List<CitationIssue> Inspect(
        Law law, Article article, List<ArticleVersion> versions, DocumentCitation citation, DateOnly? asOf,
        IEnumerable<CourtDecision> annulments)
    {
        var url = $"/{law.Slug}/{article.Slug}";
        var label = $"{law.ShortName ?? law.Number} m. {article.Number}";
        var issues = new List<CitationIssue>();
        if (versions.Count == 0)
            return [new("metin_yok", CheckSeverity.Info, $"{label}: metni kütüphaneye girilmemiş; denetlenemedi.", url)];

        var current = ArticleVersion.Select(versions, null)!;
        var shown = current;
        if (asOf is { } date)
        {
            var dateUrl = $"{url}?tarih={date:yyyy-MM-dd}";
            shown = ArticleVersion.Select(versions, date);
            if (shown is null)
            {
                var first = versions.Where(v => v.ValidFrom is not null).MinBy(v => v.ValidFrom);
                issues.Add(new("yururlukte_degil", CheckSeverity.Warning,
                    $"{label}: {Format(date)} tarihinde yürürlükte değildi"
                    + (first?.ValidFrom is { } from ? $" (ilk metin {Format(from)} tarihli)." : "."), url));
            }
            else if (shown.IsRepealed)
            {
                issues.Add(new("mulga_tarihte", CheckSeverity.Warning, $"{label}: {Format(date)} tarihinde mülgaydı.", dateUrl));
            }
            if (shown is not null && shown.Id != current.Id)
            {
                var later = versions.Where(v => v.ValidFrom > date).OrderBy(v => v.ValidFrom);
                issues.Add(new("sonradan_degisti", CheckSeverity.Warning,
                    $"{label}: {Format(date)} tarihinden sonra değişti ({Describe(later)}). Lehe kanun karşılaştırması "
                    + "(TCK m. 7) gerekebilir; bağlantı olay tarihindeki metni açar.", dateUrl));
            }
        }
        else if (current.IsRepealed)
        {
            issues.Add(new("mulga", CheckSeverity.Warning, $"{label}: mülga ({Describe([current])}).", url));
        }
        else if (versions.Count > 1)
        {
            issues.Add(new("degisiklik", CheckSeverity.Info,
                $"{label}: {versions.Count - 1} kez değişti, son değişiklik {Describe([current])}. Olay tarihini girerseniz "
                + "o tarihteki metne göre denetlenir.", $"{url}?sekme=degisiklikler"));
        }

        if (shown is not null && citation.Paragraph is { } number)
            issues.AddRange(CheckParagraph(label, shown, number, citation.Item, url));

        foreach (var decision in annulments)
        {
            issues.Add(new("aym_iptal", CheckSeverity.Warning,
                $"{label}: Anayasa Mahkemesinin {DecisionLabels.Citation(null, null, decision.DecisionDate, null, decision.CaseNumber, decision.DecisionNumber, isApplication: false)} sayılı kararıyla "
                + $"{(decision.Outcome == NormReviewOutcome.KismenIptal ? "kısmen iptal edildi" : "iptal edildi")}; "
                + "uygulanabilirliğini kontrol edin.", $"{url}?sekme=norm"));
        }

        if (issues.Count == 0)
        {
            issues.Add(new("yururlukte", CheckSeverity.Ok, asOf is { } reference
                ? $"{label}: {Format(reference)} tarihindeki metin bugün de yürürlükte."
                : $"{label}: yürürlükte.", url));
        }
        return issues;
    }

    /// <summary>Fıkra numarası ve bent harfi o sürümde var mı? Fıkralara ayrılmamış maddede yalnız "/1" geçerlidir.</summary>
    private static IEnumerable<CitationIssue> CheckParagraph(string label, ArticleVersion version, int number, string? item, string url)
    {
        var numbered = version.Paragraphs.Where(p => p.Number is not null).ToList();
        var paragraph = numbered.Count > 0
            ? numbered.FirstOrDefault(p => p.Number == number)
            : number == 1 ? version.Paragraphs.FirstOrDefault() : null;
        if (paragraph is null)
        {
            yield return new("fikra_yok", CheckSeverity.Error,
                $"{label}: {number}. fıkra yok ({(numbered.Count > 0 ? $"maddede {numbered.Count} fıkra var" : "madde fıkralara ayrılmamış")}).", url);
            yield break;
        }
        if (item is not null && !paragraph.Items.Any(i => i.Level == 1 && ItemLetter(i.Label) == TurkishText.Fold(item)))
            yield return new("bent_yok", CheckSeverity.Error, $"{label}: {number}. fıkrada ({item}) bendi yok.", url);
    }

    // "a)" / "a-" / "(a)" → "a"
    private static string ItemLetter(string? label) => TurkishText.Fold(label).Trim('(', ')', '.', '-', ' ');

    private static async Task<ILookup<Guid, CourtDecision>> AnnulmentsAsync(
        AppDbContext db, List<Guid> articleIds, bool includePrivate, CancellationToken ct)
    {
        var rows = await (
            from link in db.ArticleDecisions
            join decision in db.CourtDecisions on link.DecisionId equals decision.Id
            where articleIds.Contains(link.ArticleId)
                && decision.Kind == DecisionKind.NormDenetimi
                && (decision.Outcome == NormReviewOutcome.Iptal || decision.Outcome == NormReviewOutcome.KismenIptal)
            orderby decision.DecisionDate
            select new { link.ArticleId, Decision = decision }
        ).AsNoTracking().ToListAsync(ct);
        return rows
            .Where(r => includePrivate || r.Decision.Visibility == Visibility.Public)
            .ToLookup(r => r.ArticleId, r => r.Decision);
    }

    private static string Label(Law law, Article article, DocumentCitation citation) =>
        $"{law.ShortName ?? law.Number} m. {article.Number}"
        + (citation.Paragraph is { } p ? $"/{p}" : "")
        + (citation.Item is { } item ? $"-{item}" : "");

    /// <summary>"5377 s.K. m.12 ile 08.07.2005" ya da "08.07.2005, mülga".</summary>
    private static string Describe(IEnumerable<ArticleVersion> versions) =>
        string.Join("; ", versions.Select(v =>
            string.Join(", ", new[]
            {
                v.AmendedBy,
                v.ValidFrom is { } from ? Format(from) : null,
                v.IsRepealed ? "mülga" : null,
            }.Where(s => !string.IsNullOrWhiteSpace(s)))
            is { Length: > 0 } text ? text : "tarihi girilmemiş"));

    private static string Format(DateOnly date) => date.ToString("dd.MM.yyyy");
}
