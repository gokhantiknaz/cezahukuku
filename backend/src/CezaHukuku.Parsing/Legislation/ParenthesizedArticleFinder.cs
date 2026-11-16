using System.Text.RegularExpressions;

namespace CezaHukuku.Parsing.Legislation;

/// <param name="Start">Metinde işaretlenecek parçanın başlangıcı: numara, varsa fıkra ekiyle ("21/2").</param>
/// <param name="ArticleNumber">Madde numarası, fıkra eki olmadan ("21").</param>
public sealed record ParenthesizedArticle(int Start, int Length, string ArticleNumber);

/// <summary>
/// Kanun adı geçmeden parantez içinde yazılmış madde atıfları: "(madde 21)", "(Madde 21, 22)", "(madde 21 ve 22)",
/// "(maddeler 21, 22)", "(m. 21)", "(md.21)", "(madde 21/2)"; fıkra ve bent kelimeyle de yazılabilir: "(madde 87, fıkra 2
/// ve 4)", "(md. 87, fık. 2)", "(madde 53, fıkra 1, bent c)" (spec 0006). Hangi kanunun maddesi olduğunu çağıran belirler
/// (madde metninde maddenin kanunu, ders notunda notun kanunu ya da TCK).
/// <list type="bullet">
/// <item>Parantezin içi baştan sona bu biçimde olmalıdır; içinde tanınmayan bir parça varsa ("21-23", "TCK m. 21",
/// "geçici madde 1", "21 ila 23") o parantez hiç işaretlenmez: yanlış maddeye bağlamaktansa bağlamamak tercih edilir.</item>
/// <item>Her numara ayrı bulunur; fıkra (ve bent) eki numarayla birlikte işaretlenir ama madde numarası eksizdir.</item>
/// <item>Kelimeyle yazılan fıkra ve bent numaraları madde değildir, döndürülmez; son madde numarasına aittir
/// ("(madde 81, 82, fıkra 1)" → 81, 82).</item>
/// </list>
/// </summary>
public static partial class ParenthesizedArticleFinder
{
    public static IReadOnlyList<ParenthesizedArticle> Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var result = new List<ParenthesizedArticle>();
        foreach (Match group in Parenthesized().Matches(text))
        {
            var inner = group.Groups["inner"];
            var match = Reference().Match(inner.Value);
            if (!match.Success)
                continue;
            foreach (Capture part in match.Groups["part"].Captures)
            {
                var slash = part.Value.IndexOf('/');
                result.Add(new ParenthesizedArticle(
                    inner.Index + part.Index, part.Length, slash < 0 ? part.Value : part.Value[..slash]));
            }
        }
        return result;
    }

    // En içteki parantez: "(bkz. (madde 21))" içindeki "(madde 21)" de bulunur.
    [GeneratedRegex(@"\((?<inner>[^()]*)\)")]
    private static partial Regex Parenthesized();

    // "madde 21", "maddeler 21, 22", "m. 21", "md.21"; numaralar virgül ya da "ve" ile; fıkra eki "/2", bent "/1-c".
    // Ardından isteğe bağlı kelimeyle fıkra ("fıkra 2 ve 4", "fık. 2") ve bent ("bent c", "bendi c ve d"); bunların
    // numaraları "part" değildir. "FIKRA" ve "BENDİ" büyük harfle de tanınsın diye f[ıi]kra, bend[iİ] (değişmez kültürde I ı'ya, İ i'ye eşlenmez).
    [GeneratedRegex("""
        ^\s*
        (?:maddeler\s+|madde\s+|md\.\s*|m\.\s*)
        (?<part>\d{1,3}(?:/\d{1,2}(?:-[a-zçğıöşü])?)?)
        (?:\s*(?:,|\s+ve\s+)\s*(?<part>\d{1,3}(?:/\d{1,2}(?:-[a-zçğıöşü])?)?))*
        (?:\s*,?\s*(?:f[ıi]kra\s+|f[ıi]k\.\s*)\d{1,2}(?:\s*(?:,|\s+ve\s+)\s*\d{1,2})*)?
        (?:\s*,?\s*(?:bend[iİ]\s+|bent\s+)(?:[a-zçğıöşü]|\d{1,2})(?:\s*(?:,|\s+ve\s+)\s*(?:[a-zçğıöşü]|\d{1,2}))*)?
        \s*$
        """, RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Reference();
}
