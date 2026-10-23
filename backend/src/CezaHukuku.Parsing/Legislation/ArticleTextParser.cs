using System.Text;
using System.Text.RegularExpressions;

namespace CezaHukuku.Parsing.Legislation;

/// <param name="Number">Fıkra numarası ("(2)" → 2); numarasız fıkrada null.</param>
/// <param name="Text">Fıkranın bentlerden önceki metni.</param>
public sealed record ParsedParagraph(int? Number, string Text, IReadOnlyList<ParsedItem> Items);

/// <param name="Level">1 bent, 2 alt bent, 0 bentlerden sonra gelen bağlayıcı metin.</param>
public sealed record ParsedItem(int Level, string? Label, string Text);

/// <summary>
/// Mevzuat biçiminde yazılmış/yapıştırılmış madde metnini fıkra ve bentlere ayırır. Satır kuralları:
/// <list type="bullet">
/// <item>"(1) ..." yeni numaralı fıkra.</item>
/// <item>"a) ..." bent; "1. ..." / "1) ..." aynı fıkrada harfli bent varsa alt bent, yoksa bent.</item>
/// <item>Bentlerden sonra gelen düz satır fıkrayı bağlayan metindir ("...hâlinde ... cezalandırılır.");
/// bent yokken düz satır yeni numarasız fıkradır.</item>
/// </list>
/// Baştaki "MADDE 81 –" başlığı atılır. Boş satırlar yok sayılır.
/// </summary>
public static partial class ArticleTextParser
{
    public static IReadOnlyList<ParsedParagraph> Parse(string? text)
    {
        var paragraphs = new List<Draft>();
        Draft? current = null;
        var first = true;

        foreach (var rawLine in (text ?? "").Normalize(NormalizationForm.FormC).Split('\n'))
        {
            var line = CollapseSpaces(rawLine);
            if (first && line.Length > 0)
            {
                line = ArticleHeader().Replace(line, "", 1);
                first = false;
            }
            if (line.Length == 0)
                continue;

            if (ParagraphStart().Match(line) is { Success: true } paragraph)
            {
                current = new Draft(int.Parse(paragraph.Groups[1].Value), paragraph.Groups[2].Value);
                paragraphs.Add(current);
            }
            else if (LetterItem().Match(line) is { Success: true } letter)
            {
                current ??= Add(paragraphs, new Draft(null, ""));
                current.Items.Add(new ParsedItem(1, letter.Groups[1].Value + ")", letter.Groups[2].Value));
                current.HasLetterItems = true;
            }
            else if (current is not null && NumberItem().Match(line) is { Success: true } number)
            {
                var level = current.HasLetterItems ? 2 : 1;
                current.Items.Add(new ParsedItem(level, number.Groups[1].Value + number.Groups[2].Value, number.Groups[3].Value));
            }
            else if (current is { Items.Count: > 0 })
            {
                current.Items.Add(new ParsedItem(0, null, line));
            }
            else
            {
                current = Add(paragraphs, new Draft(null, line));
            }
        }

        return [.. paragraphs.Select(p => new ParsedParagraph(p.Number, p.Text, [.. p.Items]))];
    }

    private static Draft Add(List<Draft> paragraphs, Draft draft)
    {
        paragraphs.Add(draft);
        return draft;
    }

    private static string CollapseSpaces(string line) => Whitespace().Replace(line, " ").Trim();

    private sealed class Draft(int? number, string text)
    {
        public int? Number { get; } = number;
        public string Text { get; } = text;
        public List<ParsedItem> Items { get; } = [];
        public bool HasLetterItems { get; set; }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // "MADDE 81 –", "Madde 103/A -", "EK MADDE 1 —", "GEÇİCİ MADDE 2-"
    [GeneratedRegex(@"^((EK|GEÇİCİ|Ek|Geçici)\s+)?(MADDE|Madde)\s+[0-9]+(\s*/\s*[A-Za-z])?\s*[-–—]\s*")]
    private static partial Regex ArticleHeader();

    [GeneratedRegex(@"^\((\d+)\)\s*(.*)$")]
    private static partial Regex ParagraphStart();

    [GeneratedRegex(@"^([a-zçğıöşü])\)\s*(.*)$")]
    private static partial Regex LetterItem();

    [GeneratedRegex(@"^(\d+)([.)])\s+(.*)$")]
    private static partial Regex NumberItem();
}
