using System.Text.RegularExpressions;

namespace CezaHukuku.Parsing.Decisions;

public enum CitedCourt
{
    Yargitay,
    AnayasaMahkemesi,
    Danistay,
    BolgeAdliyeMahkemesi,
}

/// <summary>Metinde anılan karar künyesi; parçalar yazıldığı gibi değil, karşılaştırılabilir biçimde tutulur.</summary>
/// <param name="Start">Künyenin başlangıcı: mahkeme ya da daire adı, tarih ya da ilk numara (hangisi önceyse).</param>
/// <param name="Chamber">Daire ya da kurul, tek biçimde: "1. Ceza Dairesi", "Ceza Genel Kurulu".</param>
/// <param name="CaseNumber">Esas numarası ("2019/1234", CGK'da "2017/1-123"); AYM bireysel başvuruda başvuru numarası.</param>
/// <param name="IsApplication">Numara "B. No" (bireysel başvuru) olarak verilmiş; karar numarası beklenmez.</param>
/// <param name="DateText">Tarih metinde yazıldığı gibi; geçersizse ("31.02.2019") <paramref name="Date"/> null kalır.</param>
public sealed record DecisionCitation(
    int Start,
    int Length,
    CitedCourt? Court,
    string? Chamber,
    string? CaseNumber,
    string? DecisionNumber,
    bool IsApplication,
    string? DateText,
    DateOnly? Date);

/// <summary>
/// Dilekçe, karar ve makale metnindeki karar künyeleri: "Yargıtay 1. CD, 12.03.2019, 2018/1234 E., 2019/567 K.",
/// "YCGK'nın 15.10.2019 tarihli ve 2017/1-123 E., 2019/456 K. sayılı kararı", "(Y. 4. CD, E. 2019/1, K. 2020/2,
/// T. 12.03.2020)", "AYM, B. No: 2014/12345, 12/2/2016".
/// <list type="bullet">
/// <item>Künyenin çekirdeği esas / karar / başvuru numarasıdır (yıl/sıra, açıkça "E." "K." "Esas" "Karar" "B. No" ile
/// işaretli). Yalnız esas numarası verilmiş, mahkemesi ve tarihi olmayan numara ("2019/123 Esas sayılı dosya")
/// künye sayılmaz: çoğu zaman yerel mahkeme dosyasıdır.</item>
/// <item>Tarih numaraların hemen önünde ya da arkasında, aralarında yalnız ayraç ve "tarihli ve", "sayılı ve", "T."
/// gibi bağlayıcılar varsa künyeye aittir.</item>
/// <item>Mahkeme ve daire künyenin önündeki kısımda (en çok 120 karakter, noktalı virgüle ya da boş satıra kadar) son
/// anılandır. Daire Yargıtay'a özgüyse ("CD", "CGK") mahkeme yazılmasa da Yargıtay'dır.</item>
/// </list>
/// </summary>
public static partial class DecisionCitationFinder
{
    private const int CourtLookBehind = 120;
    private const int MaxNumberGap = 60;

    public static IReadOnlyList<DecisionCitation> Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var numbers = NumberMention().Matches(text).Select(ToToken).ToList();
        if (numbers.Count == 0)
            return [];
        var dates = DateMention().Matches(text)
            .Where(d => !numbers.Any(n => Overlaps(d.Index, d.Length, n.Start, n.Length)))
            .ToList();
        var groups = Group(text, numbers, dates);

        var result = new List<DecisionCitation>();
        var usedDates = new HashSet<int>();
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var lower = Math.Max(i > 0 ? groups[i - 1].End : 0, SegmentStart(text, group.Start));
            var upper = Math.Min(i < groups.Count - 1 ? groups[i + 1].Start : text.Length, SegmentEnd(text, group.End));

            var date = NearestDate(text, dates, usedDates, group, lower, upper);
            var start = Math.Min(group.Start, date?.Index ?? int.MaxValue);
            var end = Math.Max(group.End, date is null ? 0 : date.Index + date.Length);

            var (court, chamber, courtStart) = FindCourt(text, Math.Max(lower, start - CourtLookBehind), start);
            var esas = group.Get(NumberKind.Case) ?? group.Get(NumberKind.Application);
            var karar = group.Get(NumberKind.Decision);
            // Mahkemesiz, tarihsiz tek esas numarası: yerel dosya numarası olabilir.
            if (karar is null && group.Get(NumberKind.Application) is null && court is null && chamber is null && date is null)
                continue;

            if (date is not null)
                usedDates.Add(date.Index);
            start = Math.Min(start, courtStart ?? int.MaxValue);
            result.Add(new DecisionCitation(start, end - start, court, chamber, esas, karar,
                group.Get(NumberKind.Application) is not null && group.Get(NumberKind.Case) is null,
                date?.Value, date is null ? null : ParseDate(date)));
        }
        return result;
    }

    /// <summary>Kayıtlı karardaki daire adını künyedekiyle aynı biçime getirir ("1. CD" → "1. Ceza Dairesi").</summary>
    public static string? NormalizeChamber(string? chamber)
    {
        if (string.IsNullOrWhiteSpace(chamber))
            return null;
        var match = ChamberMention().Match(chamber);
        return match.Success ? ChamberName(match) : chamber.Trim();
    }

    /// <summary>Esas / karar numarasından boşlukları atar: "2017 / 1 - 123" → "2017/1-123".</summary>
    public static string NormalizeNumber(string number) => Whitespace().Replace(number, "");

    private enum NumberKind
    {
        Case,
        Decision,
        Application,
    }

    private sealed record NumberToken(int Start, int Length, NumberKind Kind, string Value);

    private sealed class NumberGroup
    {
        public List<NumberToken> Tokens { get; } = [];
        public int Start => Tokens[0].Start;
        public int End => Tokens[^1].Start + Tokens[^1].Length;
        public string? Get(NumberKind kind) => Tokens.FirstOrDefault(t => t.Kind == kind)?.Value;
    }

    private static NumberToken ToToken(Match match)
    {
        var (kind, group) = match.Groups["e"].Success ? (NumberKind.Case, "e")
            : match.Groups["k"].Success ? (NumberKind.Decision, "k")
            : (NumberKind.Application, "b");
        return new NumberToken(match.Index, match.Length, kind, NormalizeNumber(match.Groups[group].Value));
    }

    /// <summary>Art arda gelen esas, karar ve başvuru numaraları (her türden biri) aynı künyedir.</summary>
    private static List<NumberGroup> Group(string text, List<NumberToken> numbers, List<Match> dates)
    {
        var groups = new List<NumberGroup>();
        NumberGroup? current = null;
        foreach (var token in numbers)
        {
            if (current is not null
                && current.Tokens.All(t => t.Kind != token.Kind)
                && token.Start - current.End <= MaxNumberGap
                && IsConnector(WithoutDates(text, current.End, token.Start, dates)))
            {
                current.Tokens.Add(token);
                continue;
            }
            current = new NumberGroup();
            current.Tokens.Add(token);
            groups.Add(current);
        }
        return groups;
    }

    private static string WithoutDates(string text, int from, int to, List<Match> dates)
    {
        var gap = text[from..to];
        foreach (var date in dates.Where(d => d.Index >= from && d.Index + d.Length <= to).OrderByDescending(d => d.Index))
            gap = gap.Remove(date.Index - from, date.Length);
        return gap;
    }

    private static bool IsConnector(string gap) => Connector().IsMatch(gap);

    private static Match? NearestDate(string text, List<Match> dates, HashSet<int> used, NumberGroup group, int lower, int upper)
    {
        Match? best = null;
        var bestDistance = int.MaxValue;
        foreach (var date in dates)
        {
            if (used.Contains(date.Index) || date.Index < lower || date.Index + date.Length > upper)
                continue;
            int distance;
            if (date.Index + date.Length <= group.Start)
            {
                if (!IsConnector(StripCourt(text[(date.Index + date.Length)..group.Start])))
                    continue;
                distance = group.Start - (date.Index + date.Length);
            }
            else if (date.Index >= group.End)
            {
                if (!IsConnector(text[group.End..date.Index]))
                    continue;
                distance = date.Index - group.End;
            }
            else
            {
                distance = 0; // esas ile karar arasında: "E. 2018/1, T. 12.03.2019, K. 2019/2"
            }
            if (distance < bestDistance)
                (best, bestDistance) = (date, distance);
        }
        return best;
    }

    // "(Y. 1. CD, 12.03.2019, E. …)": tarih ile numara arasında daire adı da olabilir.
    private static string StripCourt(string gap) => ChamberMention().Replace(CourtMention().Replace(gap, ""), "");

    private static (CitedCourt? Court, string? Chamber, int? Start) FindCourt(string text, int from, int to)
    {
        var region = text[from..to];
        Match? court = null;
        foreach (Match m in CourtMention().Matches(region))
            court = m;
        Match? chamber = null;
        foreach (Match m in ChamberMention().Matches(region))
            chamber = m;
        if (court is null && chamber is null)
            return (null, null, null);

        CitedCourt? cited = court is null ? null : CourtOf(court);
        // "YCGK" mahkeme ile daireyi birlikte verir.
        var chamberName = chamber is not null ? ChamberName(chamber)
            : court is not null && court.Groups["ycgk"].Success ? "Ceza Genel Kurulu"
            : court is not null && court.Groups["yhgk"].Success ? "Hukuk Genel Kurulu"
            : court is not null && court.Groups["yibk"].Success ? "İçtihadı Birleştirme Genel Kurulu"
            : null;
        if (cited is null && chamber is not null
            && (chamber.Groups["cd"].Success || chamber.Groups["hd"].Success || chamber.Groups["cgk"].Success
                || chamber.Groups["hgk"].Success || chamber.Groups["ibk"].Success))
            cited = CitedCourt.Yargitay;

        var start = Math.Min(court?.Index ?? int.MaxValue, chamber?.Index ?? int.MaxValue);
        return (cited, chamberName, from + start);
    }

    private static CitedCourt CourtOf(Match court) =>
        court.Groups["aym"].Success ? CitedCourt.AnayasaMahkemesi
        : court.Groups["danistay"].Success ? CitedCourt.Danistay
        : court.Groups["bam"].Success ? CitedCourt.BolgeAdliyeMahkemesi
        : CitedCourt.Yargitay;

    private static string ChamberName(Match chamber)
    {
        var number = chamber.Groups["n"].Value;
        if (chamber.Groups["cd"].Success)
            return $"{number}. Ceza Dairesi";
        if (chamber.Groups["hd"].Success)
            return $"{number}. Hukuk Dairesi";
        if (chamber.Groups["d"].Success)
            return $"{number}. Daire";
        if (chamber.Groups["cgk"].Success)
            return "Ceza Genel Kurulu";
        if (chamber.Groups["hgk"].Success)
            return "Hukuk Genel Kurulu";
        if (chamber.Groups["ibk"].Success)
            return "İçtihadı Birleştirme Genel Kurulu";
        if (chamber.Groups["bolum"].Success)
            return $"{chamber.Groups["bolum"].Value} Bölüm";
        return "Genel Kurul";
    }

    private static readonly string[] Months =
        ["ocak", "şubat", "mart", "nisan", "mayıs", "haziran", "temmuz", "ağustos", "eylül", "ekim", "kasım", "aralık"];

    private static DateOnly? ParseDate(Match date)
    {
        var day = int.Parse(date.Groups["d"].Value);
        var month = date.Groups["mn"].Success
            ? Array.IndexOf(Months, date.Groups["mn"].Value.ToLowerInvariant()) + 1
            : int.Parse(date.Groups["m"].Value);
        var year = int.Parse(date.Groups["y"].Value);
        return year >= 1 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;
    }

    /// <summary>Künye parçalarını ayıran bölüm (noktalı virgül ya da boş satır) başlangıcı.</summary>
    private static int SegmentStart(string text, int index)
    {
        var semicolon = text.LastIndexOf(';', Math.Max(0, index - 1));
        var blank = text.LastIndexOf("\n\n", Math.Max(0, index - 1), StringComparison.Ordinal);
        return Math.Max(semicolon + 1, blank < 0 ? 0 : blank + 2);
    }

    private static int SegmentEnd(string text, int index)
    {
        var semicolon = text.IndexOf(';', index);
        var blank = text.IndexOf("\n\n", index, StringComparison.Ordinal);
        return Math.Min(semicolon < 0 ? text.Length : semicolon, blank < 0 ? text.Length : blank);
    }

    private static bool Overlaps(int a, int aLength, int b, int bLength) => a < b + bLength && b < a + aLength;

    private const string Number = @"\d{4}\s*/\s*\d{1,6}(?:\s*-\s*\d{1,6})?";

    // Esas / karar / başvuru numarası, işaretiyle: "E. 2018/1234", "Esas No: 2018/1234", "2018/1234 E.",
    // "2018/1234 Esas", "K.2019/567", "B. No: 2014/12345". İşaretsiz "2019/123" alınmaz.
    [GeneratedRegex(
        @"(?:\bE\s*[.:]\s*|\bE\s+(?=\d)|\bEsas(?:\s+(?:No|Numarası|Sayısı))?\s*[.:]?\s*)(?<e>" + Number + @")(?!\d)"
        + @"|(?<![\d/])(?<e>" + Number + @")\s*(?:E\b\.?|Esas\b)"
        + @"|(?:\bK\s*[.:]\s*|\bK\s+(?=\d)|\bKarar(?:\s+(?:No|Numarası|Sayısı))?\s*[.:]?\s*)(?<k>" + Number + @")(?!\d)"
        + @"|(?<![\d/])(?<k>" + Number + @")\s*(?:K\b\.?|Karar\b)"
        + @"|(?:\bB\.\s*No|\bBaşvuru\s+(?:No|Numarası))\s*[.:]?\s*(?<b>" + Number + @")(?!\d)")]
    private static partial Regex NumberMention();

    // "12.03.2019", "12/3/2019", "12 Mart 2019".
    [GeneratedRegex(
        @"(?<![\d/])(?<!\d\.)(?<d>\d{1,2})(?<sep>[./])(?<m>\d{1,2})\k<sep>(?<y>\d{4})(?![\d/])"
        + @"|(?<!\d)(?<d>\d{1,2})\s+(?<mn>Ocak|Şubat|Mart|Nisan|Mayıs|Haziran|Temmuz|Ağustos|Eylül|Ekim|Kasım|Aralık)\s+(?<y>\d{4})(?!\d)")]
    private static partial Regex DateMention();

    // Künye parçaları arasında yalnız ayraç ve bağlayıcı sözcükler: ", ", " tarihli ve ", " sayılı ve ", ", T. ".
    [GeneratedRegex(@"^[\s,;:()\-–]*(?:(?:sayılı|tarihli|tarihinde|tarih|ve|T\.|T:|K\.\s*T\.|Karar\s+Tarihi|Tarihi|Tarih)[\s,;:()\-–.]*)*$")]
    private static partial Regex Connector();

    [GeneratedRegex(
        @"(?<ycgk>\bYCGK\b)|(?<yhgk>\bYHGK\b)|(?<yibk>\bY[İI]B(?:B)?G?K\b)"
        + @"|(?<aym>\bAYM\b|Anayasa\s+Mahkemesi)"
        + @"|(?<danistay>Danıştay|\bD[İI]DDK\b)"
        + @"|(?<bam>Bölge\s+Adliye\s+Mahkemesi|\bBAM\b)"
        + @"|(?<yargitay>Yargıtay|\bY\.(?=\s*(?:\d|[A-ZÇĞİÖŞÜ])))")]
    private static partial Regex CourtMention();

    [GeneratedRegex(
        @"(?<cgk>Ceza\s+Genel\s+Kurulu\p{L}*|(?<!\p{L})CGK\b)"
        + @"|(?<hgk>Hukuk\s+Genel\s+Kurulu\p{L}*|(?<!\p{L})HGK\b)"
        + @"|(?<ibk>İçtihad[ıi]\s+Birleştirme\s+(?:Büyük\s+)?Genel\s+Kurulu\p{L}*)"
        + @"|(?<!\d)(?<n>\d{1,2})\s*\.?\s*(?:(?<cd>Ceza\s+Daire\p{L}*|C\.?D\b\.?)|(?<hd>Hukuk\s+Daire\p{L}*|H\.?D\b\.?)|(?<d>Daire\p{L}*))"
        + @"|(?<bolum>Birinci|İkinci)\s+Bölüm"
        + @"|(?<gk>\bGenel\s+Kurul\b)")]
    private static partial Regex ChamberMention();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
