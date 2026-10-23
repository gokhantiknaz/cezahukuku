using AngleSharp.Html.Parser;
using Ganss.Xss;

namespace CezaHukuku.Infrastructure.Content;

/// <summary>
/// Editörden gelen zengin metni (TipTap HTML'i) yalnız izin verilen etiket ve özelliklerle bırakır. Metin başka
/// kullanıcılara HTML olarak gösterildiğinden (dangerouslySetInnerHTML) script, olay özellikleri, stil ve
/// javascript: bağlantıları burada atılır; istemciye güvenilmez.
/// </summary>
public static class RichText
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();
    private static readonly HtmlParser Parser = new();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in (string[])["p", "br", "strong", "b", "em", "i", "u", "s", "h2", "h3", "h4", "ul", "ol", "li",
                     "blockquote", "a", "code", "hr", "mark"])
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowDataAttributes = false;
        // Word'den ya da web'den yapıştırılan metin <div>/<span> içinde gelir: izin verilmeyen sarmalayıcı atılır,
        // içindeki metin kalır. Tehlikeli kaplar (script vb.) içerikleriyle birlikte önceden silinir (bkz. Sanitize).
        sanitizer.KeepChildNodes = true;

        // Dış bağlantılar yeni sekmede ve opener'a erişmeden açılsın.
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement { LocalName: "a" } link && link.HasAttribute("href"))
            {
                link.SetAttribute("target", "_blank");
                link.SetAttribute("rel", "noopener noreferrer nofollow");
            }
        };
        sanitizer.AllowedAttributes.Add("target");
        sanitizer.AllowedAttributes.Add("rel");
        return sanitizer;
    }

    /// <summary>İçeriğiyle birlikte silinen öğeler: metinleri de okuyucuya gösterilmemeli.</summary>
    private const string DroppedWithContent = "script, style, iframe, object, embed, noscript, template, svg, math, head, title";

    /// <summary>Temizlenmiş HTML; içerik yoksa boş metin.</summary>
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        var document = Parser.ParseDocument($"<body>{html}</body>");
        foreach (var element in document.QuerySelectorAll(DroppedWithContent).ToList())
            element.Remove();

        var clean = Sanitizer.Sanitize(document.Body?.InnerHtml ?? "");
        if (ToPlainText(clean).Length == 0)
            return "";

        // Düzenleyicinin sonda bıraktığı boş paragraflar (<p></p>, <p><br></p>) atılır.
        var result = Parser.ParseDocument($"<body>{clean}</body>");
        foreach (var paragraph in result.QuerySelectorAll("p").ToList())
        {
            if (string.IsNullOrWhiteSpace(paragraph.TextContent) && paragraph.Children.All(c => c.LocalName == "br"))
                paragraph.Remove();
        }
        return (result.Body?.InnerHtml ?? "").Trim();
    }

    /// <summary>HTML'in düz metni; bloklar arasında boşluk korunur.</summary>
    public static string ToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";
        var document = Parser.ParseDocument($"<body>{html}</body>");
        foreach (var block in document.QuerySelectorAll("p, li, h2, h3, h4, blockquote, br"))
            block.After(document.CreateTextNode(" "));
        return string.Join(' ', (document.Body?.TextContent ?? "").Split((char[])[' ', '\n', '\r', '\t'],
            StringSplitOptions.RemoveEmptyEntries));
    }
}
