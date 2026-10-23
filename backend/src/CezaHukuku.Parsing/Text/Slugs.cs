using System.Text;
using System.Text.RegularExpressions;

namespace CezaHukuku.Parsing.Text;

/// <summary>Temiz adresler için kısa adlar: /tck/madde-81, /cgtik/gecici-madde-2.</summary>
public static partial class Slugs
{
    /// <summary>Metni aksansız, küçük harfli, tireyle ayrılmış hâle getirir: "CGTİK" → "cgtik".</summary>
    public static string FromText(string? text)
    {
        var folded = TurkishText.Fold(text);
        var sb = new StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }
        return sb.ToString().TrimEnd('-');
    }

    public static bool IsValid(string? slug) => slug is not null && ValidSlug().IsMatch(slug);

    /// <summary>
    /// Madde numarasından kanun içinde tekil adres parçası: "81" → "madde-81", "103/A" → "madde-103-a",
    /// "Ek 1" / "Ek Madde 1" → "ek-madde-1", "Geçici 2" → "gecici-madde-2".
    /// </summary>
    public static string ForArticle(string number)
    {
        var slug = StripPrefix(FromText(number), "madde-");
        foreach (var kind in (string[])["ek-", "gecici-"])
        {
            if (slug.StartsWith(kind, StringComparison.Ordinal))
                return kind + "madde-" + StripPrefix(slug[kind.Length..], "madde-");
        }
        return "madde-" + slug;
    }

    private static string StripPrefix(string text, string prefix) =>
        text.StartsWith(prefix, StringComparison.Ordinal) ? text[prefix.Length..] : text;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValidSlug();
}
