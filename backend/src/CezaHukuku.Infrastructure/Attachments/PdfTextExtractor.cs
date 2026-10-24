using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CezaHukuku.Infrastructure.Attachments;

/// <summary>PDF'in her sayfasının metni (metin katmanından). Taranmış sayfada metin boş döner.</summary>
public static class PdfTextExtractor
{
    public static List<string> ExtractPages(Stream pdf)
    {
        using var document = PdfDocument.Open(pdf);
        var pages = new List<string>(document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            // İçerik sırasına göre çıkarıcı satır ve sütunları okuma sırasında birleştirir.
            var text = ContentOrderTextExtractor.GetText(page);
            pages.Add(Normalize(text));
        }
        return pages;
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
