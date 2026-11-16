using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Content;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Legislation;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Library;

/// <summary>
/// Madde metnindeki parantez içi atıf ("(madde 21, 22)" içindeki "21"): fıkra/bent içindeki konumu ve açılacak madde.
/// </summary>
/// <param name="ItemId">Atıf bentteyse bendin kimliği; fıkranın kendi metnindeyse null.</param>
/// <param name="AccessibleName">Ekran okuyucu için ad: "TCK madde 21'i aç".</param>
public sealed record ArticleReferenceSpan(
    Guid ParagraphId, Guid? ItemId, int Start, int Length, LinkedArticleRef Target, string AccessibleName);

/// <summary>
/// Parantez içi madde atıflarını (<see cref="ParenthesizedArticleFinder"/>) okuyucunun görebildiği maddelere bağlar
/// (spec 0005). Atıf hep tek bir kanuna aittir: madde metninde maddenin kendi kanunu, ders notunda notun kanunu ya da
/// TCK. Görünürlük yeni bir yerde denetlenmez: kanunun okuyucuya görünen ağacı (<see cref="LibraryEndpoints.LoadOutlineAsync"/>)
/// kullanılır; ağaçta olmayan (yok ya da görünmeyen) madde bağlanmaz, iz de bırakmaz (BR-1).
/// </summary>
internal static class ArticleReferences
{
    /// <summary>Kanunu belirsiz ders notlarında atıfın kanunu: Türk Ceza Kanunu (adres adı tekildir, numara değil).</summary>
    public const string DefaultLawSlug = "tck";

    /// <summary>Madde numarası → okuyucunun görebildiği madde ("21" → TCK 21).</summary>
    public static Dictionary<string, LinkedArticleRef> TargetsOf(Law law, LawOutline outline) =>
        outline.ReadingOrder
            .GroupBy(a => a.Article.Number, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => VisibleArticles.ToRef(g.First().Article, law), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gösterilen sürümün fıkra ve bent metinlerindeki atıflar. Otomatik atıfla (<paramref name="citations"/>) çakışan
    /// parça atlanır: aynı metin iki bağlantı olmaz.
    /// </summary>
    public static List<ArticleReferenceSpan> ForVersion(
        ArticleVersion version, Law law, LawOutline outline, IReadOnlyList<CitationSpan> citations)
    {
        Dictionary<string, LinkedArticleRef>? targets = null;
        var spans = new List<ArticleReferenceSpan>();

        void Collect(Guid paragraphId, Guid? itemId, string text)
        {
            foreach (var found in ParenthesizedArticleFinder.Find(text))
            {
                targets ??= TargetsOf(law, outline);
                if (!targets.TryGetValue(found.ArticleNumber, out var target))
                    continue;
                var overlaps = citations.Any(c => c.ParagraphId == paragraphId && c.ItemId == itemId
                    && found.Start < c.Start + c.Length && c.Start < found.Start + found.Length);
                if (!overlaps)
                    spans.Add(new ArticleReferenceSpan(paragraphId, itemId, found.Start, found.Length, target,
                        ArticleReferenceHtml.AccessibleName(target.LawLabel, target.Number)));
            }
        }

        foreach (var paragraph in version.Paragraphs)
        {
            Collect(paragraph.Id, null, paragraph.Text);
            foreach (var item in paragraph.Items)
                Collect(paragraph.Id, item.Id, item.Text);
        }
        return spans;
    }

    /// <summary>
    /// Ders notundaki atıfların kanunu ve okuyucuya görünen ağacı: not bir kanuna (ya da maddeye; o zaman kanunu da
    /// doludur) bağlıysa o kanun, değilse ya da yalnız gruba bağlıysa TCK. Kanun yoksa ya da okuyucuya kapalıysa null.
    /// </summary>
    public static async Task<(Law Law, LawOutline Outline)?> LawForNoteAsync(
        AppDbContext db, LectureNote note, bool includePrivate)
    {
        var slug = DefaultLawSlug;
        if (note.LawId is { } lawId)
        {
            var linked = await db.Laws.AsNoTracking().Where(l => l.Id == lawId).Select(l => l.Slug).FirstOrDefaultAsync();
            if (linked is null)
                return null;
            slug = linked;
        }
        return await LibraryEndpoints.LoadOutlineAsync(db, slug, includePrivate);
    }

    /// <summary>
    /// Ders notunun okuma HTML'i: atıflar notun kanununda okuyucunun görebildiği maddelere bağlanır. Metinde atıf yoksa
    /// kanun hiç yüklenmez; kanun belirlenemezse HTML olduğu gibi döner.
    /// </summary>
    public static async Task<string> ReadingHtmlAsync(AppDbContext db, LectureNote note, bool includePrivate)
    {
        if (ParenthesizedArticleFinder.Find(note.PlainText).Count == 0
            || await LawForNoteAsync(db, note, includePrivate) is not ({ } law, { } outline))
            return note.Html;

        var targets = TargetsOf(law, outline);
        return ArticleReferenceHtml.Mark(note.Html, number => targets.TryGetValue(number, out var t)
            ? new ArticleReferenceTarget(t.LawSlug, t.ArticleSlug, t.LawLabel, t.Number)
            : null);
    }
}
