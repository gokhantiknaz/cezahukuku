using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CezaHukuku.Parsing.Legislation;

public enum ParsedNodeKind
{
    Kitap,
    Kisim,
    Bolum,
    Ayrim,
}

/// <param name="Number">Kanunun kendi numarası ("5237"); künyeden okunur.</param>
public sealed record LawMetadata(string? Number, DateOnly? AcceptedOn, DateOnly? GazetteDate, string? GazetteNumber);

/// <param name="Order">Aynı üst öğedeki düğüm ve maddeler arasında metindeki sırası.</param>
public sealed record ParsedNode(
    ParsedNodeKind Kind,
    string Number,
    string Title,
    int Order,
    IReadOnlyList<ParsedNode> Children,
    IReadOnlyList<ParsedArticle> Articles);

/// <param name="Number">"81", "103/A", "Ek 1", "Geçici 2".</param>
/// <param name="LastAmendedOn">Metindeki değişiklik notlarının ("(Değişik: 12/5/2022-7406/2 md.)") en yeni tarihi;
/// not yoksa null. Bu tarih değiştiren kanunun kabul tarihidir, yürürlük tarihi birkaç gün sonra olabilir.</param>
/// <param name="LastAmendment">O notun kendisi: "Değişik: 12/5/2022-7406/2 md.".</param>
public sealed record ParsedArticle(
    string Number,
    string Title,
    bool IsRepealed,
    int Order,
    IReadOnlyList<ParsedParagraph> Paragraphs,
    DateOnly? LastAmendedOn = null,
    string? LastAmendment = null);

/// <param name="Articles">Hiçbir düğüme bağlı olmayan maddeler (bölümlemesiz kanunlar).</param>
/// <param name="FootnoteCount">Metnin sonundaki dipnot sayısı (dipnotlar şimdilik içe aktarılmaz).</param>
public sealed record ParsedLaw(
    LawMetadata Metadata,
    IReadOnlyList<ParsedNode> Nodes,
    IReadOnlyList<ParsedArticle> Articles,
    IReadOnlyList<string> Warnings,
    int FootnoteCount);

public sealed record LawTextParserOptions
{
    /// <summary>
    /// PDF'ten kopyalanan metinde cümle ortasında kırılan satırları birleştirir: küçük harfle başlayan ve yapı
    /// satırı olmayan satır bir öncekinin devamı sayılır. mevzuat.gov.tr'nin web görünümünden kopyalanan metinde
    /// kapalı kalmalı; orada küçük harfle başlayan satır bentlerden sonraki bağlayıcı metindir ("yürürlüğe girer.").
    /// </summary>
    public bool JoinWrappedLines { get; init; }
}

/// <summary>
/// Bir kanunun tam metnini (mevzuat.gov.tr biçimi) ağaç ve maddelere ayırır. Kurallar gerçek metinden çıkarıldı:
/// <list type="bullet">
/// <item>"BİRİNCİ KİTAP" / "İKİNCİ KISIM" / "DOKUZUNCU BÖLÜM" / "… AYRIM" düğümdür; sonraki satır başlığıdır.
/// Düğümler Kitap › Kısım › Bölüm › Ayrım sırasıyla iç içe yerleşir.</item>
/// <item>"Madde 1-", "Madde 61. -", "MADDE 9 –", "Geçici Madde 1 –", "Ek Madde 2-" maddeyi başlatır.</item>
/// <item>Madde satırından hemen önceki, nokta/virgülle bitmeyen kısa satır maddenin başlığıdır.</item>
/// <item>Gövdesi yalnız "(Mülga: …)" olan madde mülgadır.</item>
/// <item>"[12]" dipnot işaretleri atılır; "[1] …" ile başlayan dipnotlar ve "… SAYILI KANUNA …" ile başlayan ek
/// listeler (başka kanunların geçici maddeleri dahil) kanun metninin sonu sayılır.</item>
/// <item>İlk düğüm ya da maddeden önceki satırlar künyedir (Kanun Numarası, Kabul Tarihi, Resmî Gazete).</item>
/// </list>
/// Fıkra ve bentler <see cref="ArticleTextParser"/> ile ayrılır.
/// </summary>
public static partial class LawTextParser
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static ParsedLaw Parse(string? text, LawTextParserOptions? options = null)
    {
        options ??= new LawTextParserOptions();
        var warnings = new List<string>();
        var lines = PrepareLines(text, options, out var footnoteCount);

        var rootNodes = new List<NodeDraft>();
        var rootArticles = new List<ArticleDraft>();
        var stack = new List<NodeDraft>();
        var rootOrder = 0;
        NodeDraft? awaitingTitle = null;
        ArticleDraft? article = null;
        string? pendingTitle = null;
        var preamble = new List<string>();
        var started = false;

        void FinishArticle()
        {
            article?.Finish(warnings);
            article = null;
        }

        int NextOrder() => stack.Count > 0 ? stack[^1].NextOrder() : rootOrder++;

        foreach (var line in lines)
        {
            if (HeadingMatch(line) is { } heading)
            {
                started = true;
                FinishArticle();
                if (pendingTitle is not null)
                    warnings.Add($"Başlık olarak kullanılamayan satır atlandı: “{Shorten(pendingTitle)}”");
                pendingTitle = null;
                if (awaitingTitle is not null)
                    warnings.Add($"“{awaitingTitle.Number}” düğümünün başlığı bulunamadı.");

                // Aynı ya da daha alt seviyedeki açık düğümler kapanır.
                while (stack.Count > 0 && stack[^1].Kind >= heading.Kind)
                    stack.RemoveAt(stack.Count - 1);
                var node = new NodeDraft(heading.Kind, heading.Number, NextOrder());
                (stack.Count > 0 ? stack[^1].Children : rootNodes).Add(node);
                stack.Add(node);
                awaitingTitle = node;
                continue;
            }

            var articleHeader = ArticleHeaderMatch(line);
            if (awaitingTitle is not null && articleHeader is null)
            {
                awaitingTitle.Title = line;
                awaitingTitle = null;
                continue;
            }
            awaitingTitle = null;

            if (articleHeader is { } header)
            {
                // Bölümsüz kanunda ilk maddenin başlığı künyenin hemen ardından gelir ("Amaç").
                if (!started && preamble.Count > 0 && IsTitleCandidate(preamble[^1]) && !preamble[^1].Contains(':'))
                {
                    pendingTitle = preamble[^1];
                    preamble.RemoveAt(preamble.Count - 1);
                }
                started = true;
                var title = pendingTitle ?? article?.TakeTitleCandidate() ?? "";
                pendingTitle = null;
                FinishArticle();
                article = new ArticleDraft(header.Number, title, NextOrder());
                if (header.Rest.Length > 0)
                    article.Body.Add(header.Rest);
                (stack.Count > 0 ? stack[^1].Articles : rootArticles).Add(article);
                continue;
            }

            if (!started)
            {
                preamble.Add(line);
            }
            else if (article is not null)
            {
                article.Body.Add(line);
            }
            else
            {
                // Düğüm başlığından sonra, ilk maddeden önce gelen satır: ilk maddenin başlığı.
                if (pendingTitle is not null)
                    warnings.Add($"Başlık olarak kullanılamayan satır atlandı: “{Shorten(pendingTitle)}”");
                pendingTitle = line;
            }
        }

        FinishArticle();
        if (awaitingTitle is not null)
            warnings.Add($"“{awaitingTitle.Number}” düğümünün başlığı bulunamadı.");

        var nodes = rootNodes.Select(n => n.Build()).ToList();
        var articles = rootArticles.Select(a => a.Build()).ToList();
        if (nodes.Count == 0 && articles.Count == 0)
            warnings.Add("Metinde madde bulunamadı. Maddeler “Madde 1-” biçiminde başlamalı.");

        return new ParsedLaw(ParseMetadata(preamble), nodes, articles, warnings, footnoteCount);
    }

    private static List<string> PrepareLines(string? text, LawTextParserOptions options, out int footnoteCount)
    {
        var lines = new List<string>();
        footnoteCount = 0;
        var ended = false;

        foreach (var raw in (text ?? "").Normalize(NormalizationForm.FormC).Split('\n'))
        {
            // Dipnot satırı ("[1] …") işaretler silinmeden önce tanınmalı.
            if (FootnoteLine().IsMatch(raw.TrimStart()))
            {
                footnoteCount++;
                ended = true;
                continue;
            }

            var line = Whitespace().Replace(FootnoteMarker().Replace(raw, ""), " ").Trim();
            if (line.Length == 0)
                continue;
            if (ended || IsAppendixStart(line))
            {
                ended = true;
                continue;
            }

            if (options.JoinWrappedLines && lines.Count > 0 && IsWrappedContinuation(line))
                lines[^1] += " " + line;
            else
                lines.Add(line);
        }
        return lines;
    }

    /// <summary>"5237 SAYILI KANUNA İŞLENEMEYEN HÜKÜMLER", "… SAYILI KANUNA EK VE DEĞİŞİKLİK GETİREN …".</summary>
    private static bool IsAppendixStart(string line) =>
        line.Contains("SAYILI KANUNA", StringComparison.Ordinal) && line == line.ToUpper(Turkish);

    private static bool IsWrappedContinuation(string line) =>
        char.IsLower(line[0])
        && !BentStart().IsMatch(line)
        && HeadingMatch(line) is null
        && ArticleHeaderMatch(line) is null;

    private sealed record Heading(ParsedNodeKind Kind, string Number);

    private static Heading? HeadingMatch(string line)
    {
        if (line.Length > 60)
            return null;
        var match = HeadingPattern().Match(line.ToUpper(Turkish));
        if (!match.Success)
            return null;

        var kind = match.Groups[2].Value switch
        {
            "KİTAP" => ParsedNodeKind.Kitap,
            "KISIM" => ParsedNodeKind.Kisim,
            "BÖLÜM" => ParsedNodeKind.Bolum,
            _ => ParsedNodeKind.Ayrim,
        };
        return new Heading(kind, TitleCase(line));
    }

    private sealed record ArticleHeader(string Number, string Rest);

    private static ArticleHeader? ArticleHeaderMatch(string line)
    {
        var match = ArticleHeaderPattern().Match(line);
        if (!match.Success)
            return null;

        var prefix = match.Groups["kind"].Value.ToLower(Turkish) switch
        {
            "ek" => "Ek ",
            "geçici" => "Geçici ",
            _ => "",
        };
        var number = match.Groups["no"].Value;
        if (match.Groups["sub"].Success)
            number += "/" + match.Groups["sub"].Value.ToUpper(Turkish);
        return new ArticleHeader(prefix + number, match.Groups["rest"].Value.Trim());
    }

    private static LawMetadata ParseMetadata(List<string> preamble)
    {
        var text = string.Join("\n", preamble);
        static DateOnly? Date(Match m) =>
            m.Success && DateOnly.TryParseExact(m.Groups[1].Value, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : null;

        var number = MetaNumber().Match(text);
        var gazetteNumber = MetaGazetteNumber().Match(text);
        return new LawMetadata(
            number.Success ? number.Groups[1].Value : null,
            Date(MetaAccepted().Match(text)),
            Date(MetaGazetteDate().Match(text)),
            gazetteNumber.Success ? gazetteNumber.Groups[1].Value : null);
    }

    /// <summary>"İKİNCİ KISIM" → "İkinci Kısım" (Türkçe büyük/küçük harf kurallarıyla).</summary>
    private static string TitleCase(string text) =>
        string.Join(' ', text.ToLower(Turkish).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpper(word[0], Turkish) + word[1..]));

    /// <summary>Madde başlığı olabilecek satır: kısa, nokta/virgülle bitmeyen, fıkra ya da bent olmayan.</summary>
    private static bool IsTitleCandidate(string line) =>
        line.Length <= 200 && !".,;:".Contains(line[^1]) && !BentStart().IsMatch(line) && !ParagraphStart().IsMatch(line);

    private static string Shorten(string text) => text.Length <= 60 ? text : text[..57] + "…";

    private sealed class NodeDraft(ParsedNodeKind kind, string number, int order)
    {
        private int nextOrder;

        public ParsedNodeKind Kind { get; } = kind;
        public string Number { get; } = number;
        public string Title { get; set; } = "";
        public List<NodeDraft> Children { get; } = [];
        public List<ArticleDraft> Articles { get; } = [];

        public int NextOrder() => nextOrder++;

        public ParsedNode Build() =>
            new(Kind, Number, Title, order, [.. Children.Select(c => c.Build())], [.. Articles.Select(a => a.Build())]);
    }

    private sealed class ArticleDraft(string number, string title, int order)
    {
        private IReadOnlyList<ParsedParagraph> paragraphs = [];
        private bool isRepealed;
        private (DateOnly Date, string Note)? lastAmendment;

        public List<string> Body { get; } = [];

        /// <summary>
        /// Gövdenin son satırı bir sonraki maddenin başlığıysa onu gövdeden çıkarıp döndürür: kısa, nokta/virgülle
        /// bitmeyen ve fıkra/bent olmayan satır. Gövdede başka satır yoksa dokunulmaz.
        /// </summary>
        public string? TakeTitleCandidate()
        {
            if (Body.Count < 2)
                return null;
            var last = Body[^1];
            if (!IsTitleCandidate(last))
                return null;
            Body.RemoveAt(Body.Count - 1);
            return last;
        }

        public void Finish(List<string> warnings)
        {
            paragraphs = ArticleTextParser.Parse(string.Join('\n', Body));
            isRepealed = Body.Count == 1 && Body[0].StartsWith("(Mülga", StringComparison.OrdinalIgnoreCase);
            lastAmendment = LatestAmendment(Body);
            if (paragraphs.Count == 0)
                warnings.Add($"Madde {number}: metin bulunamadı.");
        }

        public ParsedArticle Build() =>
            new(number, title, isRepealed, order, paragraphs, lastAmendment?.Date, lastAmendment?.Note);
    }

    /// <summary>
    /// "(Değişik: 12/5/2022-7406/2 md.)", "(Ek cümle: 29/6/2005 – 5377/7 md.)", "(Mülga – 17/4/2013-6460/13 md.)",
    /// "(İptal: Anayasa Mahkemesinin 8/10/2015 tarihli …)" notlarından en yeni tarihlisi.
    /// </summary>
    private static (DateOnly Date, string Note)? LatestAmendment(IEnumerable<string> lines)
    {
        (DateOnly Date, string Note)? latest = null;
        foreach (Match note in lines.SelectMany(line => AmendmentNote().Matches(line)))
        {
            var date = NoteDate().Match(note.Groups[1].Value);
            if (date.Success
                && DateOnly.TryParseExact(date.Value, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                && (latest is null || parsed > latest.Value.Date))
            {
                latest = (parsed, Whitespace().Replace(note.Groups[1].Value, " ").Trim());
            }
        }
        return latest;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\[\d{1,4}\]")]
    private static partial Regex FootnoteMarker();

    [GeneratedRegex(@"^\[\d{1,4}\]\s")]
    private static partial Regex FootnoteLine();

    // Sıra sayısı -(I)NCI ekiyle biter: BİRİNCİ, ÜÇÜNCÜ, ALTINCI, DOKUZUNCU, ON BİRİNCİ…
    [GeneratedRegex(@"^([A-ZÇĞİÖŞÜ ]*?(?:İNCİ|INCI|UNCU|ÜNCÜ|NCİ|NCI|NCU|NCÜ))\s+(KİTAP|KISIM|BÖLÜM|AYRIM)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^(?:(?<kind>Ek|EK|Geçici|GEÇİCİ)\s+)?(?:Madde|MADDE)\s+(?<no>\d+)(?:\s*/\s*(?<sub>[A-Za-zÇĞİÖŞÜçğıöşü]))?\s*\.?\s*[-–—]\s*(?<rest>.*)$")]
    private static partial Regex ArticleHeaderPattern();

    [GeneratedRegex(@"^\(\d+\)")]
    private static partial Regex ParagraphStart();

    [GeneratedRegex(@"^([a-zçğıöşü]\)|\d+[.)]\s)")]
    private static partial Regex BentStart();

    [GeneratedRegex(@"\(((?:Değişik|Ek|Mülga|İptal|Yeniden düzenleme)[^()]*)\)")]
    private static partial Regex AmendmentNote();

    [GeneratedRegex(@"\d{1,2}/\d{1,2}/\d{4}")]
    private static partial Regex NoteDate();

    [GeneratedRegex(@"Kanun Numarası\s*:\s*(\d+)")]
    private static partial Regex MetaNumber();

    [GeneratedRegex(@"Kabul Tarihi\s*:\s*(\d{1,2}/\d{1,2}/\d{4})")]
    private static partial Regex MetaAccepted();

    [GeneratedRegex(@"Resm[iî] Gazete\s*:\s*Tarih\s*:\s*(\d{1,2}/\d{1,2}/\d{4})")]
    private static partial Regex MetaGazetteDate();

    [GeneratedRegex(@"Resm[iî] Gazete\s*:.*?Sayı\s*:\s*(\d+)", RegexOptions.Singleline)]
    private static partial Regex MetaGazetteNumber();
}
