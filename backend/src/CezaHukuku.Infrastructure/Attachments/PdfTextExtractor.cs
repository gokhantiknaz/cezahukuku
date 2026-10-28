using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CezaHukuku.Infrastructure.Attachments;

/// <summary>PDF'in her sayfasının metni (metin katmanından). Taranmış sayfada metin boş döner.</summary>
public static class PdfTextExtractor
{
    /// <param name="markFootnoteSeparator">Sayfa altındaki dipnotları ayıran çizgi metinde "_____" satırı olarak
    /// kalsın (kanun içe aktarmada dipnotun sayfa sonunda bölünen devamını ayırt etmek için).</param>
    public static List<string> ExtractPages(Stream pdf, bool markFootnoteSeparator = false)
    {
        using var document = PdfDocument.Open(pdf);
        var pages = new List<string>(document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            // İçerik sırasına göre çıkarıcı satır ve sütunları okuma sırasında birleştirir.
            var text = Normalize(ContentOrderTextExtractor.GetText(page));
            pages.Add(markFootnoteSeparator ? MarkFootnoteSeparator(page, text) : text);
        }
        return pages;
    }

    /// <summary>
    /// Word'den üretilen PDF'te (mevzuat.gov.tr) dipnot çizgisi uzun bir boşluk dizisidir; çıkarıcı onu atar. Dizinin
    /// ardından gelen ilk harfler metinde bulunup önüne "_____" satırı konur.
    /// </summary>
    private static string MarkFootnoteSeparator(Page page, string text)
    {
        var letters = page.Letters;
        var run = 0;
        var after = -1;
        for (var i = 0; i < letters.Count && after < 0; i++)
        {
            run = string.IsNullOrWhiteSpace(letters[i].Value) ? run + 1 : 0;
            if (run >= 20)
            {
                after = i + 1;
                while (after < letters.Count && string.IsNullOrWhiteSpace(letters[after].Value))
                    after++;
            }
        }
        if (after < 0 || after >= letters.Count)
            return text;

        // Çizgiden sonraki ilk 20 harf (boşluksuz) metinde nerede başlıyor?
        var probe = string.Concat(letters.Skip(after).Select(l => l.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Take(20));
        var compact = new System.Text.StringBuilder();
        var positions = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
                continue;
            compact.Append(text[i]);
            positions.Add(i);
        }
        var found = compact.ToString().LastIndexOf(probe, StringComparison.Ordinal);
        if (probe.Length == 0 || found < 0)
            return text;
        var start = positions[found];
        var lineStart = text.LastIndexOf('\n', Math.Max(start - 1, 0)) + 1;
        return text[..lineStart] + "_____\n" + text[lineStart..];
    }

    /// <summary>Satır sonu tirelemesini birleştirir ("öl-\ndürme" → "öldürme"), boşlukları sadeleştirir.</summary>
    public static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToList();
        var result = new System.Text.StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.EndsWith('-') && i + 1 < lines.Count && lines[i + 1].Length > 0 && char.IsLower(lines[i + 1][0]))
            {
                result.Append(line[..^1]);
                continue;
            }
            result.Append(line);
            if (i + 1 < lines.Count)
                result.Append('\n');
        }
        return result.ToString().Trim();
    }
}
