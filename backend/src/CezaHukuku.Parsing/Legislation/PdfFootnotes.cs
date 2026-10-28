using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CezaHukuku.Parsing.Legislation;

/// <summary>
/// PDF'ten (kopyala-yapıştır ya da dosyadan) alınan kanun metnindeki dipnotları mevzuat.gov.tr'nin web biçimine
/// çevirir. PDF'te dipnot işareti üst simgedir; metne geçince önündeki sözcüğe yapışır ("SEKİZİNCİ BAP41",
/// "olamaz.5", yan yana iki işaret "uygulanmaz.910"). Dipnotun kendisi sayfanın altındadır; metinde o sayfanın son
/// satırından sonra "41 Bu başlık, 22/12/1934 tarih ve 2632 sayılı Kanunun 9 uncu maddesi ile gelmiştir." satırı
/// olarak araya girer, ardından sonraki sayfanın metni sürer.
/// <para>
/// Dipnotlar kanun boyunca 1'den başlayıp sırayla numaralanır. Bir satır yalnız sıradaki numarayla başlıyor ve o
/// numara aynı sayfanın metninde bir sözcüğe yapışık (işaret) olarak geçiyorsa dipnot sayılır; böylece "1 - …" gibi
/// bentler ya da metindeki sayılar dipnot sanılmaz. Dipnot, cümlesi bitene kadar sonraki satırlara yayılır.
/// </para>
/// Sonuç: işaretler "[41]" olur, dipnotlar metnin sonuna "[41] …" satırları olarak taşınır (<see cref="LawTextParser"/>
/// bu biçimi okur). Metin zaten web biçimindeyse ya da dipnot bulunmazsa olduğu gibi döner.
/// </summary>
public static partial class PdfFootnotes
{
    /// <param name="warnings">Sayfa sonunda bölünüp devamı bulunamayan dipnotlar buraya yazılır.</param>
    public static string Normalize(string text, ICollection<string>? warnings = null)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Any(line => WebFootnoteLine().IsMatch(line.TrimStart())))
            return text;

        var body = new List<string>();
        var footnotes = new List<(int Number, StringBuilder Text)>();
        // Sıradaki dipnotların işaretleri son iki dipnot kümesi (sayfa altı) arasındaki gövde satırlarındadır: Word
        // sayfaya sığmayan dipnotu işaretinden bir sonraki sayfanın altına kaydırabilir.
        var segmentStart = 0;
        var previousSegmentStart = 0;
        var expected = 1;
        // "[n]" olarak işaretlenmiş dipnotlar ("12" yapışık yazılınca 1 ile birlikte 2 de işaretlenir).
        var marked = new HashSet<int>();
        StringBuilder? open = null;
        // Sayfa sonunda yarım kalan dipnot: devamı sonraki sayfanın dipnot çizgisinden hemen sonra gelir.
        (int Number, StringBuilder Text)? cut = null;
        var afterSeparator = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var line = raw.Trim();
            if (FootnoteStart(line, expected) is { } footnoteText && MarkFootnote(body, previousSegmentStart, expected, marked))
            {
                if (cut is { } unfinished && !afterSeparator)
                {
                    if (TakeContinuation(body, segmentStart) is { } rest)
                        unfinished.Text.Append(' ').Append(rest);
                    else
                        warnings?.Add($"{unfinished.Number} numaralı dipnot sayfa sonunda bölünmüş, devamı ayırt edilemedi: devamı bir maddenin metnine karışmış olabilir; önizlemede kontrol edin.");
                }
                (cut, afterSeparator) = (null, false);
                open = new StringBuilder(footnoteText);
                footnotes.Add((expected, open));
                expected++;
                continue;
            }

            if (open is not null)
            {
                if (line.Length == 0 || IsSeparator(raw))
                    continue;
                var complete = IsComplete(open.ToString(), line);
                // Bentli dipnot ("a - … değiştirilmiştir." "b - …"): her bent kendi cümlesiyle biter, dipnot sürer.
                if (complete && ListFootnote().IsMatch(open.ToString()) && ListFootnote().IsMatch(line))
                    complete = false;
                // Sonraki sayfa madde başlığıyla başlayabilir: ardından "Madde N-" gelen, büyük harfle başlayıp
                // noktalamasız biten kısa satır başlıktır (dipnot satırları sayfa genişliğindedir).
                var titleOfNextArticle = i + 1 < lines.Length
                    && LawTextParser.IsArticleHeaderLine(lines[i + 1].Trim())
                    && line.Length <= 80 && char.IsUpper(line[0]) && !".,;".Contains(line[^1]);
                if (!complete && !titleOfNextArticle && !LawTextParser.IsStructureLine(line))
                {
                    open.Append(' ').Append(line);
                    continue;
                }
                // Dipnot bitti ya da bitmeden madde/bölüm başladı (dipnot sayfa sonunda bölünmüş): sonraki sayfanın
                // metni geliyor.
                if (!complete)
                    cut = footnotes[^1];
                open = null;
                (previousSegmentStart, segmentStart) = (segmentStart, body.Count);
            }

            if (cut is { } dangling)
            {
                if (IsSeparator(raw))
                {
                    afterSeparator = true;
                    continue;
                }
                if (afterSeparator && line.Length > 0)
                {
                    dangling.Text.Append(' ').Append(line);
                    if (ClosingVerb().IsMatch(dangling.Text.ToString()))
                        (cut, afterSeparator) = (null, false);
                    continue;
                }
            }
            // Sayfa altındaki dipnotları ayıran çizgi metne boşluk ya da alt çizgi dizisi olarak geçer.
            body.Add(IsSeparator(raw) ? "" : raw);
        }

        if (footnotes.Count == 0)
            return text;
        var result = new StringBuilder(string.Join('\n', body).TrimEnd()).Append("\n\n");
        foreach (var (number, footnote) in footnotes)
            result.Append('[').Append(number).Append("] ").Append(footnote).Append('\n');
        return result.ToString();
    }

    /// <summary>
    /// Sıradaki dipnotun işaretini son sayfaların gövde satırlarında arar ve "[n]" yapar. Önceki bir dipnotla birlikte
    /// yapışık yazılmış ("910") ve o sırada zaten işaretlenmişse de bulunmuş sayılır.
    /// </summary>
    private static bool MarkFootnote(List<string> body, int segmentStart, int number, HashSet<int> marked)
    {
        if (marked.Contains(number))
            return true;
        var marker = $"[{number}]";
        for (var i = segmentStart; i < body.Count; i++)
        {
            var line = body[i];
            foreach (Match run in GluedNumber().Matches(line))
            {
                var digits = run.Groups[1].Success ? run.Groups[1] : run.Groups[2];
                if (Split(digits.Value, number) is { } numbers)
                {
                    body[i] = line[..digits.Index] + string.Concat(numbers.Select(n => $"[{n}]")) + line[(digits.Index + digits.Length)..];
                    marked.UnionWith(numbers);
                    return true;
                }
            }
            // Tarayıcıdan kopyalanınca üst simge kendi satırına düşebilir: "Askeri şahıslar:" ⏎ "3".
            if (line.Trim() is { Length: > 0 } alone && alone.All(char.IsAsciiDigit) && i > segmentStart
                && Split(alone, number) is { } own)
            {
                var previous = i - 1;
                while (previous > segmentStart && body[previous].Trim().Length == 0)
                    previous--;
                body[previous] = body[previous].TrimEnd() + string.Concat(own.Select(n => $"[{n}]"));
                body[i] = "";
                marked.UnionWith(own);
                return true;
            }
            // Büyük harfli başlıkta işaret boşlukla ayrılmış olabilir: "SEKİZİNCİ BAP 41".
            if (HeadingWithNumber().Match(line.TrimEnd()) is { Success: true } heading
                && heading.Groups[2].Value == number.ToString(CultureInfo.InvariantCulture))
            {
                body[i] = line.TrimEnd()[..heading.Groups[2].Index].TrimEnd() + marker;
                marked.Add(number);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Bölünmüş dipnotun devamı, dipnot çizgisi metne geçmemişse: sonraki dipnottan hemen önce, boş satırdan sonra gelen
    /// ve dipnot gibi biten ("…kaldırılmıştır.") kısa satır kümesi gövdeden alınır.
    /// </summary>
    private static string? TakeContinuation(List<string> body, int segmentStart)
    {
        var end = body.Count;
        while (end > segmentStart && body[end - 1].Trim().Length == 0)
            end--;
        var start = end;
        while (start > segmentStart && body[start - 1].Trim().Length > 0)
            start--;
        if (start == end || start == segmentStart || end - start > 6)
            return null;
        var block = body.GetRange(start, end - start).Select(l => l.Trim()).ToList();
        var text = string.Join(' ', block);
        if (!ClosingVerb().IsMatch(text) || LawTextParser.IsStructureLine(block[0]) || block[0].StartsWith('('))
            return null;
        body.RemoveRange(start, body.Count - start);
        return text;
    }

    /// <summary>
    /// Satır <paramref name="number"/> numaralı dipnotla başlıyorsa dipnotun metni: "41 Bu başlık …" ya da numarası
    /// tarihe yapışık "552/3/2014 tarihli …" (55 ve "2/3/2014 …"). "1 - …" gibi bent satırları dipnot değildir.
    /// </summary>
    private static string? FootnoteStart(string line, int number)
    {
        var prefix = number.ToString(CultureInfo.InvariantCulture);
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
            return null;
        var rest = line[prefix.Length..];
        if (rest.Length > 1 && char.IsWhiteSpace(rest[0]) && FootnoteTextStart().IsMatch(rest.TrimStart()))
            return rest.Trim();
        // Satırın kendisi tarihle başlıyorsa ("22/3/2000 tarih ve …", bir dipnotun devamı) numara yapışık değildir.
        return DateStart().IsMatch(rest) && !DateStart().IsMatch(line) ? rest.Trim() : null;
    }

    /// <summary>"910", 9'dan başlayarak ardışık numaralara bölünürse [9, 10]; "41" ve 41 → [41]; bölünemezse null.</summary>
    private static List<int>? Split(string digits, int first)
    {
        var numbers = new List<int>();
        var rest = digits;
        for (var n = first; rest.Length > 0; n++)
        {
            var part = n.ToString(CultureInfo.InvariantCulture);
            if (!rest.StartsWith(part, StringComparison.Ordinal))
                return null;
            numbers.Add(n);
            rest = rest[part.Length..];
        }
        return numbers;
    }

    /// <summary>
    /// Dipnot bu satırdan önce bitti mi? Değişiklik notları "…değiştirilmiştir." / "…bakınız." diye biter; başka
    /// bir noktayla bitmişse sonraki satır büyük harf, parantez ya da madde/başlık ile başlıyorsa biter. Küçük harfle
    /// başlayan satır dipnotun devamıdır.
    /// </summary>
    private static bool IsComplete(string footnote, string next)
    {
        if (ClosingVerb().IsMatch(footnote))
            return true;
        if (!footnote.EndsWith('.'))
            return false;
        return !char.IsLower(next[0]);
    }

    /// <summary>Dipnot çizgisi: uzun boşluk dizisi ya da alt çizgi/tire dizisi (kırpılmamış satır).</summary>
    private static bool IsSeparator(string raw)
    {
        var line = raw.Trim();
        return line.Length == 0
            ? raw.Length >= 20
            : line.Length >= 5 && line.All(c => c is '_' or '-' or '–' or '—' or ' ');
    }

    [GeneratedRegex(@"^\[\d{1,4}\]\s")]
    private static partial Regex WebFootnoteLine();

    // Dipnot metni harf, rakam ya da tırnakla başlar: "Bu başlık …", "8/8/2026 tarihli …". "- …" (bent) değil.
    [GeneratedRegex(@"^[\p{L}\d""“'‘]")]
    private static partial Regex FootnoteTextStart();

    [GeneratedRegex(@"^\d{1,2}/\d{1,2}/\d{4}\b")]
    private static partial Regex DateStart();

    // Harfli bent: "a -Bu Kanunda …", "b - Bu Kanundaki …".
    [GeneratedRegex(@"^[a-zçğıöşü]\s?-\s?\S")]
    private static partial Regex ListFootnote();

    // Harfe ya da noktalamaya yapışık sayı: "BAP41", "olamaz.5", "md.)4", "uygulanmaz.910"; ya da satır sonunda
    // noktalamadan sonra boşlukla ayrılmış: "deyiminden; 50". "29/6/2005", "Ek:8/8/2026", "5377/5" gibi tarih ve
    // sayılar değil (arkasından "/" ya da rakam gelir).
    [GeneratedRegex(@"(?:(?<=[\p{L}.,;:)”""'’»])(\d{1,6})(?=[\s.,;:)”""'’]|$)|(?<=[.,;:)]\s)(\d{1,6})\s*$)")]
    private static partial Regex GluedNumber();

    [GeneratedRegex(@"^([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜ ]{3,60})\s+(\d{1,4})$")]
    private static partial Regex HeadingWithNumber();

    [GeneratedRegex(@"(?:m[ıiuü]ş[td][ıiuü]r|bakınız|bkz)\.\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ClosingVerb();
}
