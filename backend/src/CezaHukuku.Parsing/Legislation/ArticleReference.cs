using System.Text.RegularExpressions;
using CezaHukuku.Parsing.Text;

namespace CezaHukuku.Parsing.Legislation;

/// <param name="Law">Kanunu belirten parça, normalize: "tck", "5237", "cmk".</param>
/// <param name="ArticleNumber">Madde numarası: "81", "103/A", "Geçici 1".</param>
public sealed record ArticleReference(string Law, string ArticleNumber);

/// <summary>
/// Kısa madde göndermesi: "tck 81", "TCK m.81", "TCK md. 81", "5237/81", "765 448", "cmk geçici 1", "tck 103/a".
/// Hızlı aramada ve madde seçiminde doğrudan maddeye gitmek için kullanılır.
/// </summary>
public static partial class ArticleReferenceParser
{
    public static ArticleReference? Parse(string? text)
    {
        var normalized = TurkishText.Normalize(text);
        if (normalized.Length == 0)
            return null;

        var match = Reference().Match(normalized);
        if (!match.Success)
            return null;

        var number = match.Groups["no"].Value.ToUpperInvariant();
        var kind = match.Groups["kind"].Value switch
        {
            "geçici" or "gecici" => "Geçici ",
            "ek" => "Ek ",
            _ => "",
        };
        return new ArticleReference(TurkishText.Fold(match.Groups["law"].Value), kind + number);
    }

    // kanun: harf/rakam (tck, 5237, cgtik); ayraç: boşluk ya da "/"; isteğe bağlı "m." / "md." / "madde";
    // isteğe bağlı "geçici" / "ek"; numara: 81, 103/a.
    [GeneratedRegex(@"^(?<law>[\p{L}\d]+)\s*(?:/|\s)\s*(?:(?:m|md|madde)\.?\s*)?(?:(?<kind>geçici|gecici|ek)\s+(?:madde\s+)?)?(?<no>\d+(?:/[\p{L}])?)$")]
    private static partial Regex Reference();
}
