using System.Text.RegularExpressions;
using CezaHukuku.Parsing.Text;

namespace CezaHukuku.Parsing.Legislation;

public enum CitationLawKind
{
    /// <summary>Metnin ait olduğu kanun ("13 üncü maddede", "bu Kanunun 53 üncü maddesi").</summary>
    Self,

    /// <summary>Numarasıyla anılan kanun ("5271 sayılı Kanunun 231 inci maddesi").</summary>
    Number,

    /// <summary>Adıyla anılan kanun ("Ceza Muhakemesi Kanununun 236 ncı maddesi").</summary>
    Name,
}

/// <param name="Start">Metindeki başlangıç (bağlantı yapılacak kısım).</param>
/// <param name="LawValue">Kanun numarası ("5271") ya da adı ("Ceza Muhakemesi Kanunu"); Self'te null.</param>
/// <param name="ArticleNumber">"81", "Geçici 1", "Ek 2".</param>
public sealed record Citation(int Start, int Length, CitationLawKind LawKind, string? LawValue, string ArticleNumber);

/// <summary>
/// Mevzuat metnindeki madde atıflarını bulur: "13 üncü maddede", "11 ve 12 nci maddelerde", "279 uncu ve 280 inci
/// maddeler", "6831 sayılı Orman Kanununun 108 inci maddesinin", "geçici 1 inci madde". Kurallar gerçek TCK metninden
/// çıkarıldı.
/// <list type="bullet">
/// <item>Atıftan hemen önce kanun anılıyorsa ("… Kanununun 108 inci") o kanundur.</item>
/// <item>Anılmıyorsa aynı cümlede en son anılan kanundur: "Ceza Muhakemesi Kanununun kamu davasının açılmasının
/// ertelenmesine ilişkin 171 inci maddesi veya … ilişkin 231 inci maddesi" → ikisi de CMK.</item>
/// <item>Cümlede kanun anılmıyorsa metnin kendi kanunudur.</item>
/// <item>Hedefi belirsiz atıflar ("Anayasanın 38 inci maddesi", "Yönetmeliğin 5 inci maddesi", "aynı Kanunun …")
/// bağlanmaz: yanlış maddeye bağlamaktansa bağlamamak tercih edilir.</item>
/// </list>
/// </summary>
public static partial class CitationParser
{
    public static IReadOnlyList<Citation> Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var result = new List<Citation>();
        foreach (Match match in Reference().Matches(text))
        {
            // Liste içindeki sıra ekleri isteğe bağlı; "madde"den hemen önce ek olmalı ("5 madde hâlinde" atıf değildir).
            if (!OrdinalBeforeArticle().IsMatch(match.Value))
                continue;
            if (ResolveOwner(text[..match.Index]) is not { } owner)
                continue;

            var kind = match.Groups["kind"].Success
                ? (TurkishText.Fold(match.Groups["kind"].Value) == "gecici" ? "Geçici " : "Ek ")
                : "";
            var numbers = match.Groups["no"].Captures;
            var parts = match.Groups["part"].Captures;
            for (var i = 0; i < numbers.Count; i++)
            {
                var number = numbers[i];
                // Son numara "… nci maddesinin" ile birlikte, öncekiler kendi sıra ekleriyle ("279 uncu") bağlanır.
                var length = i == numbers.Count - 1 ? match.Index + match.Length - number.Index : parts[i].Length;
                result.Add(new Citation(number.Index, length, owner.Kind, owner.Value, kind + number.Value));
            }
        }
        return result;
    }

    private static (CitationLawKind Kind, string? Value)? ResolveOwner(string before)
    {
        // Atıfın içinde bulunduğu cümle (ya da bent): son nokta, noktalı virgül, iki nokta ya da satır sonundan sonrası.
        var clauseStart = before.LastIndexOfAny(['.', ';', ':', '\n']) + 1;
        var clause = before[clauseStart..];

        Match? last = null;
        foreach (Match owner in Owner().Matches(clause))
            last = owner;
        if (last is null)
            return (CitationLawKind.Self, null);

        if (last.Groups["number"].Success)
            return (CitationLawKind.Number, last.Groups["number"].Value);
        if (last.Groups["this"].Success)
            return (CitationLawKind.Self, null);
        if (last.Groups["named"].Success && LawName(last.Groups["named"].Value) is { } name)
            return (CitationLawKind.Name, name);
        // Kanun dışı bir metnin (Anayasa, yönetmelik, sözleşme…) ya da adı verilmemiş bir kanunun ("aynı Kanunun") maddesi.
        return null;
    }

    /// <summary>
    /// Kanunun adı, "Kanunun"dan geriye büyük harfle başlayan kelimeler ve "ve/ile" bağlaçlarıdır:
    /// "hakim tarafından Ceza Muhakemesi" → "Ceza Muhakemesi Kanunu".
    /// </summary>
    private static string? LawName(string wordsBeforeKanun)
    {
        var words = wordsBeforeKanun.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var start = words.Length;
        while (start > 0 && (char.IsUpper(words[start - 1][0]) || words[start - 1] is "ve" or "ile"))
            start--;
        while (start < words.Length && words[start] is "ve" or "ile")
            start++;
        return start < words.Length ? $"{string.Join(' ', words[start..])} Kanunu" : null;
    }

    private const string Ordinal = "(?:inci|ıncı|uncu|üncü|nci|ncı|ncu|ncü)";

    // "11 ve 12 nci maddelerde", "279 uncu ve 280 inci maddeler", "geçici 1 inci madde", "53 üncü maddesinin".
    [GeneratedRegex(@"(?<![\p{L}\d/.])(?:(?<kind>[Gg]eçici|[Ee]k)\s+)?(?<part>(?<no>\d{1,3})(?:\s*['’]?\s*" + Ordinal + @")?)(?:\s*(?:,|ve|ile|ila|veya)\s*(?<part>(?<no>\d{1,3})(?:\s*['’]?\s*" + Ordinal + @")?))*\s+madde\p{L}*")]
    private static partial Regex Reference();

    [GeneratedRegex(Ordinal + @"\s+madde\p{L}*$")]
    private static partial Regex OrdinalBeforeArticle();

    // Cümlede anılan "sahip": numaralı kanun, bu Kanun, adıyla kanun ya da kanun dışı metin.
    [GeneratedRegex(
        @"(?<number_owner>(?<!\d)(?<number>\d{3,5})\s+sayılı\s+(?:[\p{L}'’]+\s+){0,10}?Kanun\p{L}*)"
        + @"|(?<this>\b[Bb]u\s+Kanun\p{L}*)"
        + @"|(?<other>\b(?:aynı|anılan|söz konusu)\s+Kanun\p{L}*|\b(?:Anayasa|Yönetmeli|Tüzü|Sözleşme|Kararname|Protokol|Tebliğ|Genelge|Yönerge)\p{L}*)"
        // "Kanun"dan hemen önceki kelime büyük harfle başlar ("Ceza Muhakemesi Kanununun"); "ile bu Kanunun"
        // burada eşleşmez, "bu Kanun" olarak tanınır.
        + @"|(?<named>(?:[\p{L}'’]+\s+){0,9}?\p{Lu}[\p{L}'’]*\s+)Kanun(?:unun|un)\b"
        // Adı ya da numarası tanınamayan kanun ("… sayılı Kanunun", "Kanunun 5 inci maddesi"): hedef belirsiz.
        + @"|(?<other_law>\b[Kk]anun(?:unun|un)\b)")]
    private static partial Regex Owner();
}
