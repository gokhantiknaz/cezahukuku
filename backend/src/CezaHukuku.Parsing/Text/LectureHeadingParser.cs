using System.Globalization;
using System.Text.RegularExpressions;

namespace CezaHukuku.Parsing.Text;

/// <summary>İçe aktarılacak bir başlık.</summary>
/// <param name="Line">Kaynak metindeki satır numarası (1'den).</param>
/// <param name="Depth">Düzey (birinci düzey 1).</param>
/// <param name="SourceNumber">Satırdaki numara ("2.1.1"); kaydedilmez.</param>
/// <param name="Number">Sistemin vereceği numara (bölüm numarası dahil).</param>
public sealed record ImportedHeading(int Line, string Title, int Depth, string SourceNumber, string Number);

/// <param name="Line">Kaynak metindeki satır numarası; metnin tamamıyla ilgiliyse 0.</param>
/// <param name="Field">Sorunun ait olduğu form alanı: <see cref="TextField"/> ya da <see cref="TitleField"/>.</param>
public sealed record HeadingImportIssue(int Line, string Message, string Field = HeadingImportIssue.TextField)
{
    public const string TextField = "text";
    public const string TitleField = "title";
}

/// <summary>Başlık listesinin okunuşu: kullanılan ad ve bölüm numarası, öneriler, başlıklar, hatalar ve uyarılar.</summary>
/// <param name="Title">Kullanılan ders adı (öneriler uygulandıysa öneriden).</param>
/// <param name="ChapterNumber">Kullanılan bölüm numarası.</param>
/// <param name="SuggestedTitle">Metinden önerilen ders adı (bölüm satırı ya da ilk başlık bilgisi satırı).</param>
/// <param name="SuggestedChapterNumber">Metinde tek bölüm satırı varsa onun numarası.</param>
/// <param name="Errors">Kayıt yapılamaz.</param>
/// <param name="Warnings">Kayıt yapılabilir; yönetici bilsin.</param>
public sealed record HeadingImport(
    string Title,
    int? ChapterNumber,
    string? SuggestedTitle,
    int? SuggestedChapterNumber,
    IReadOnlyList<ImportedHeading> Headings,
    IReadOnlyList<HeadingImportIssue> Errors,
    IReadOnlyList<HeadingImportIssue> Warnings);

/// <summary>
/// Ders başlık listesini okur (spec 0004 R-4, R-5). Satır satır:
/// <list type="bullet">
/// <item>Numaralı satır ("1.5.1. Başlık") bir başlıktır; düzeyi numaranın parça sayısından (bölüm numarası varsa bir eksik).</item>
/// <item>Bölüm satırı ("2. BÖLÜM – SUÇ TEORİSİ"): tek bölüm satırı varsa dersin kendisidir (ad ve bölüm numarası
/// önerisi); birden çok varsa (bütün kitap) her biri birinci düzey başlıktır, ders numarasızdır.</item>
/// <item>İlk başlıktan önceki numarasız satırlar başlık bilgisidir (ilki ad önerisi); sonrakiler bir önceki başlığın
/// alt satıra kaymış devamıdır. Yalnız sayıdan oluşan satır (sayfa numarası) ve boş satır yok sayılır.</item>
/// <item>Tamamı büyük harfli ad Türkçe kurala göre kelime başları büyük olacak biçimde düzeltilir.</item>
/// </list>
/// Satırdaki numara kaydedilmez: numarayı sistem ağaçtaki yerden hesaplar; farklıysa uyarı verilir.
/// </summary>
public static partial class LectureHeadingParser
{
    public const int MaxHeadings = 300;
    public const int MaxTitleLength = 300;
    public const int MaxTextLength = 200_000;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly HashSet<string> Conjunctions = ["ve", "veya", "ile", "ya", "da", "de", "ki"];

    [GeneratedRegex(@"^(\d{1,2})\s*\.?\s*b[öo]l[üu]m\s*[-–—:]\s*(\S.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChapterLine();

    [GeneratedRegex(@"^(\d{1,3}(?:\.\d{1,3})*)(?:\.\s*|\s+)(\S.*)$")]
    private static partial Regex NumberedLine();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex PageNumber();

    private sealed class Item(int line, bool isChapter, int[] parts, string title)
    {
        public int Line { get; } = line;
        public bool IsChapter { get; } = isChapter;
        public int[] Parts { get; } = parts;
        public string Title { get; set; } = title;
    }

    /// <param name="applyTitleSuggestion">Metinden önerilen ad kullanılsın (metin değişti ve yönetici adı değiştirmedi).
    /// Metinde öneri yoksa verilen ad kalır.</param>
    /// <param name="applyChapterSuggestion">Metinden önerilen bölüm numarası kullanılsın (metin değişti ve yönetici
    /// numarayı değiştirmedi); metinde tek bölüm satırı yoksa numarasız.</param>
    public static HeadingImport Parse(
        string? text, string? title, int? chapterNumber, bool applyTitleSuggestion, bool applyChapterSuggestion)
    {
        text ??= "";
        if (text.Length > MaxTextLength)
            return new(Clean(title), chapterNumber, null, null, [],
                [new(0, "Metin 200.000 karakterden uzun olamaz.")], []);

        var items = new List<Item>();
        string? header = null;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = Clean(lines[i]);
            if (line.Length == 0 || PageNumber().IsMatch(line))
                continue;
            if (ChapterLine().Match(line) is { Success: true } chapter)
                items.Add(new(i + 1, true, [int.Parse(chapter.Groups[1].Value, CultureInfo.InvariantCulture)], chapter.Groups[2].Value));
            else if (NumberedLine().Match(line) is { Success: true } numbered)
                items.Add(new(i + 1, false, [.. numbered.Groups[1].Value.Split('.').Select(p => int.Parse(p, CultureInfo.InvariantCulture))],
                    numbered.Groups[2].Value));
            else if (items.Count == 0)
                header ??= line;
            else
                items[^1].Title += " " + line;
        }

        var chapters = items.Where(i => i.IsChapter).ToList();
        var suggestedChapter = chapters.Count == 1 ? chapters[0].Parts[0] : (int?)null;
        var suggestedTitle = chapters.Count == 1 ? FixCase(chapters[0].Title) : header is null ? null : FixCase(header);
        if (applyTitleSuggestion)
            title = suggestedTitle ?? title;
        if (applyChapterSuggestion)
            chapterNumber = suggestedChapter;
        title = Clean(title);

        var errors = new List<HeadingImportIssue>();
        var warnings = new List<HeadingImportIssue>();
        if (title.Length == 0)
            errors.Add(new(0, "Ders adı boş olamaz.", HeadingImportIssue.TitleField));
        else if (title.Length > MaxTitleLength)
            errors.Add(new(0, $"Ders adı {MaxTitleLength} karakterden uzun olamaz.", HeadingImportIssue.TitleField));

        var headings = new List<ImportedHeading>();
        var lastDepth = 0;
        var prefix = chapterNumber?.ToString(CultureInfo.InvariantCulture);
        foreach (var item in items)
        {
            var source = string.Join('.', item.Parts);
            int depth;
            if (item.IsChapter && chapterNumber is not null)
            {
                // Tek bölümlü metinde bölüm satırı dersin kendisidir.
                if (item.Parts[0] != chapterNumber)
                    errors.Add(new(item.Line, $"{source}. bölüm satırı; bölüm numarası {chapterNumber}."));
                continue;
            }
            if (item.IsChapter)
                depth = 1;
            else if (chapterNumber is { } chapter)
            {
                if (item.Parts[0] != chapter)
                {
                    errors.Add(new(item.Line, $"{source} numarası {chapter} ile başlamıyor (bölüm numarası {chapter})."));
                    continue;
                }
                depth = item.Parts.Length - 1;
                if (depth == 0)
                {
                    errors.Add(new(item.Line, $"{source} bölüm düzeyinde; bölüm numarası varken başlıklar {chapter}.1 biçiminde olmalı."));
                    continue;
                }
            }
            else
                depth = item.Parts.Length;

            var name = FixCase(item.Title);
            if (depth > lastDepth + 1)
            {
                errors.Add(new(item.Line, lastDepth == 0 && chapterNumber is null && depth == 2
                    ? $"{source} birinci düzeyde değil; metin bir bölüme aitse bölüm numarası verin."
                    : $"{source}: düzey atlanmış ({depth}. düzeyden önce {depth - 1}. düzey yok)."));
                depth = lastDepth + 1;
            }
            if (name.Length > MaxTitleLength)
                errors.Add(new(item.Line, $"Başlık {MaxTitleLength} karakterden uzun olamaz."));

            // Numara: üst başlığın numarası (birinci düzeyde bölüm numarası) + kardeşler arasındaki sıra.
            var parent = depth == 1 ? prefix : headings.Last(h => h.Depth == depth - 1).Number;
            var position = NextPosition(headings, depth).ToString(CultureInfo.InvariantCulture);
            var number = parent is null ? position : $"{parent}.{position}";
            lastDepth = depth;
            if (number != source)
                warnings.Add(new(item.Line, $"Satırdaki numara {source}, sistemin vereceği numara {number}."));
            headings.Add(new(item.Line, name, depth, source, number));
        }

        if (headings.Count == 0 && errors.All(e => e.Line == 0))
            errors.Add(new(0, "Metinde başlık bulunamadı (numaralı satır yok)."));
        if (headings.Count > MaxHeadings)
            errors.Add(new(0, $"En çok {MaxHeadings} başlık aktarılabilir; metinde {headings.Count} başlık var."));
        warnings.AddRange(DuplicateSiblings(headings));

        return new(title, chapterNumber, suggestedTitle, suggestedChapter, headings, errors, warnings);
    }

    /// <summary>Tamamı büyük harfli adı Türkçe kurala göre kelime başları büyük yapar; karışık harfli ad aynen kalır.</summary>
    public static string FixCase(string title)
    {
        title = Clean(title);
        if (!title.Any(char.IsLetter) || title.Any(char.IsLower))
            return title;
        var words = title.ToLower(Turkish).Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0 && Conjunctions.Contains(words[i]))
                continue;
            // Baştaki noktalamadan sonraki ilk harf ("(nato" → "(Nato").
            var index = words[i].ToList().FindIndex(char.IsLetter);
            if (index >= 0)
                words[i] = string.Concat(words[i][..index], words[i][index].ToString().ToUpper(Turkish), words[i][(index + 1)..]);
        }
        return string.Join(' ', words);
    }

    private static int NextPosition(List<ImportedHeading> headings, int depth)
    {
        // Geriye doğru: aynı düzeyde kardeş bulunursa onun sırası + 1; daha üst düzeye varılırsa 1.
        for (var i = headings.Count - 1; i >= 0; i--)
        {
            if (headings[i].Depth < depth)
                return 1;
            if (headings[i].Depth == depth)
                return int.Parse(headings[i].Number.Split('.')[^1], CultureInfo.InvariantCulture) + 1;
        }
        return 1;
    }

    private static IEnumerable<HeadingImportIssue> DuplicateSiblings(List<ImportedHeading> headings)
    {
        // Üst başlığa göre gruplama: numaranın son parçası hariç kısmı aynı olan başlıklar kardeştir.
        static string ParentOf(string number) => number.Contains('.') ? number[..number.LastIndexOf('.')] : "";
        return headings
            .GroupBy(h => (Parent: ParentOf(h.Number), Title: TurkishText.Normalize(h.Title)))
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Skip(1).Select(h => new HeadingImportIssue(h.Line,
                $"Aynı üst başlık altında aynı ad: \"{h.Title}\" ({string.Join(", ", g.Select(x => x.Number))}).")));
    }

    private static string Clean(string? text) => string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
