using System.Text.RegularExpressions;

namespace CezaHukuku.Parsing.Legislation;

public enum DocumentLawKind
{
    /// <summary>Kısaltmayla: "TCK m. 81", "CMK'nın 231. maddesi".</summary>
    Abbreviation,

    /// <summary>Numarayla: "5271 sayılı Kanunun 231 inci maddesi".</summary>
    Number,

    /// <summary>Adla: "Ceza Muhakemesi Kanununun 236 ncı maddesi".</summary>
    Name,
}

/// <param name="LawValue">Kısaltma ("TCK"), kanun numarası ("5271") ya da adı.</param>
/// <param name="Paragraph">Kısa biçimde verilen fıkra: "TCK 53/1-c" → 1.</param>
/// <param name="Item">Kısa biçimde verilen bent: "TCK 53/1-c" → "c".</param>
public sealed record DocumentCitation(
    int Start, int Length, DocumentLawKind Kind, string LawValue, string ArticleNumber, int? Paragraph = null, string? Item = null);

/// <summary>
/// Belge (karar, makale, ders notu PDF'i) metnindeki madde atıfları. Kanun metninin aksine belgede "bu Kanun" yoktur;
/// kanunu belli olmayan atıflar ("13 üncü madde") alınmaz. Hukuk yazımındaki kısa biçimler de bulunur:
/// "TCK 81", "TCK m. 81", "TCK md.81", "TCK'nın 81. maddesi", "CMK’nun 231 inci maddesi"; kısa biçimde fıkra ve bent
/// de verilebilir: "TCK 81/2", "TCK m. 53/1-c". Hangi kısaltmanın hangi kanun
/// olduğu ve maddenin var olup olmadığı okuma anında kütüphaneden çözülür (bilinmeyen kısaltma bağlanmaz).
/// </summary>
public static partial class DocumentCitationFinder
{
    public static IReadOnlyList<DocumentCitation> Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var found = CitationParser.Find(text)
            .Where(c => c.LawKind != CitationLawKind.Self && c.LawValue is not null)
            .Select(c => new DocumentCitation(c.Start, c.Length,
                c.LawKind == CitationLawKind.Number ? DocumentLawKind.Number : DocumentLawKind.Name, c.LawValue!, c.ArticleNumber))
            .ToList();

        foreach (Match match in ShortReference().Matches(text))
        {
            var start = match.Index;
            var end = match.Index + match.Length;
            if (found.Any(c => start < c.Start + c.Length && c.Start < end))
                continue;
            // "AYM 2019/12": dört haneli numara yıldır, arkasındaki esas sırası fıkra değildir.
            if (match.Groups["para"].Success && match.Groups["no"].Length == 4)
                continue;
            found.Add(new DocumentCitation(start, match.Length, DocumentLawKind.Abbreviation,
                match.Groups["law"].Value, match.Groups["no"].Value.ToUpperInvariant(),
                match.Groups["para"].Success ? int.Parse(match.Groups["para"].Value) : null,
                match.Groups["item"].Success ? match.Groups["item"].Value : null));
        }
        return [.. found.OrderBy(c => c.Start)];
    }

    // Kısaltma (2-6 büyük harf) + isteğe bağlı ek ('nın, ’nun) + isteğe bağlı "m." / "md." / "madde(si)" + numara
    // ("81", "103/a") + isteğe bağlı fıkra ve bent ("/2", "/1-c") + isteğe bağlı sıra eki ve "madde…" ("81. maddesi",
    // "231 inci maddesi").
    // Numara 4 haneden uzunsa alınmaz; dört haneli numaranın arkasındaki "/rakam" (yıl ve esas sırası: "AYM 2019/123")
    // fıkra sayılmaz.
    [GeneratedRegex("""
        (?<![\p{L}\d])
        (?<law>[A-ZÇĞİÖŞÜ]{2,6})
        (?:['’](?:n[ıiuü]n|[ıiuü]n|[ıiuü]|n[ae]|[ae]|d[ae]|t[ae]))?
        \s*
        (?:(?:m|md|mad)\.\s*|madde(?:si(?:nin|ne|nde)?)?\s+)?
        (?<no>\d{1,4}(?:/[a-zA-Z](?![\p{L}]))?)
        (?:/(?<para>\d{1,2})(?:\s*-\s*(?<item>[a-zçğıöşü])(?![\p{L}]))?)?
        (?![\d/])
        (?:\s*(?:\.|['’]?(?:inci|ıncı|uncu|üncü|nci|ncı|ncu|ncü|ci|cı|cu|cü))\s+madde[\p{L}]*)?
        """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex ShortReference();
}
