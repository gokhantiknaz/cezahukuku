using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CezaHukuku.Parsing.Legislation;

namespace CezaHukuku.Infrastructure.Content;

/// <summary>Parantez içi atıfın açacağı madde: okuma adresi /{LawSlug}/{ArticleSlug}.</summary>
/// <param name="LawLabel">Kanunun kısaltması ya da numarası ("TCK"); erişilebilir addaki ilk kelime.</param>
public sealed record ArticleReferenceTarget(string LawSlug, string ArticleSlug, string LawLabel, string Number);

/// <summary>
/// Ders notunun okuma HTML'i (spec 0005): temizlenmiş HTML'in metnindeki parantez içi atıfların
/// (<see cref="ParenthesizedArticleFinder"/>) her numarası, açılacak maddeye giden bir bağlantıyla sarılır:
/// <c>&lt;a href="/tck/madde-21" class="article-ref" data-article-ref="tck/madde-21" aria-label="TCK madde 21'i aç"&gt;21&lt;/a&gt;</c>.
/// Kaydedilen HTML'e dokunulmaz; bu yalnız okumada üretilir. Metin aynen kalır; var olan bir bağlantının içindeki metin
/// sarılmaz (iç içe bağlantı olmaz). Çözülemeyen numara (yok ya da okuyucuya görünmüyor) düz metin kalır.
/// </summary>
public static class ArticleReferenceHtml
{
    private static readonly HtmlParser Parser = new();

    /// <param name="resolve">Madde numarası → madde; okuyucunun göremediği ya da olmayan madde için null.</param>
    public static string Mark(string html, Func<string, ArticleReferenceTarget?> resolve)
    {
        if (string.IsNullOrEmpty(html) || !html.Contains('('))
            return html;

        var document = Parser.ParseDocument($"<body>{html}</body>");
        var body = document.Body!;
        var changed = false;
        foreach (var text in TextNodes(body).ToList())
        {
            if (text.ParentElement?.Closest("a") is not null)
                continue;

            var value = text.Data;
            var pieces = new List<INode>();
            var position = 0;
            foreach (var found in ParenthesizedArticleFinder.Find(value))
            {
                if (resolve(found.ArticleNumber) is not { } target)
                    continue;
                if (found.Start > position)
                    pieces.Add(document.CreateTextNode(value[position..found.Start]));
                var link = document.CreateElement("a");
                link.SetAttribute("href", $"/{target.LawSlug}/{target.ArticleSlug}");
                link.SetAttribute("class", "article-ref");
                link.SetAttribute("data-article-ref", $"{target.LawSlug}/{target.ArticleSlug}");
                link.SetAttribute("aria-label", AccessibleName(target.LawLabel, target.Number));
                link.TextContent = value.Substring(found.Start, found.Length);
                pieces.Add(link);
                position = found.Start + found.Length;
            }
            if (pieces.Count == 0)
                continue;
            if (position < value.Length)
                pieces.Add(document.CreateTextNode(value[position..]));
            text.Replace([.. pieces]);
            changed = true;
        }
        return changed ? body.InnerHtml : html;
    }

    /// <summary>"TCK madde 21'i aç": numaranın okunuşuna göre belirtme hâli eki ("22'yi", "30'u", "100'ü").</summary>
    public static string AccessibleName(string lawLabel, string number) =>
        $"{lawLabel} madde {number}'{AccusativeSuffix(number)} aç";

    private static string AccusativeSuffix(string number)
    {
        var digits = number.TrimEnd();
        if (digits.Length == 0 || !char.IsDigit(digits[^1]))
            return "i";
        // Okunuşun son kelimesi: son sıfır olmayan basamak; sondaki sıfırlar onlar, yüzler, binler kelimesine götürür.
        var zeros = digits.Length - digits.TrimEnd('0').Length;
        if (zeros == digits.Length)
            return "ı"; // "0" → sıfır'ı
        if (zeros == 0)
            return digits[^1] switch
            {
                '1' or '5' or '8' => "i", // bir, beş, sekiz
                '2' or '7' => "yi", // iki, yedi
                '3' or '4' => "ü", // üç, dört
                '6' => "yı", // altı
                _ => "u", // dokuz
            };
        if (zeros == 1)
            return digits[^2] switch
            {
                '1' or '3' => "u", // on, otuz
                '2' or '5' => "yi", // yirmi, elli
                '4' or '6' or '9' => "ı", // kırk, altmış, doksan
                _ => "i", // yetmiş, seksen
            };
        return zeros == 2 ? "ü" : "i"; // yüz; bin
    }

    private static IEnumerable<IText> TextNodes(INode node)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText text)
                yield return text;
            else
                foreach (var inner in TextNodes(child))
                    yield return inner;
        }
    }
}
