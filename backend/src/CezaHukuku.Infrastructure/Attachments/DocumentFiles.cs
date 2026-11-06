using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace CezaHukuku.Infrastructure.Attachments;

/// <param name="Extension">Depoda kullanılan uzantı (".pdf").</param>
/// <param name="Inline">Tarayıcıda açılır (PDF, resim); değilse indirilir.</param>
public sealed record DocumentFileType(string Extension, string ContentType, bool Inline);

/// <param name="PageCount">PDF sayfa sayısı.</param>
public sealed record DocumentText(string Text, int? PageCount);

/// <summary>
/// Paylaşılan belge türleri (ders notu ekleri): PDF, Word, PowerPoint, Excel, resim. Tür, dosya adının uzantısı ve
/// içeriğin ilk baytlarıyla birlikte belirlenir (uzantısı değiştirilmiş dosya kabul edilmez). HTML, SVG gibi tarayıcıda
/// betik çalıştırabilecek türler kabul edilmez.
/// </summary>
public static partial class DocumentFiles
{
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] Ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];

    private static readonly Dictionary<string, (string ContentType, byte[] Magic, bool Inline)> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", Pdf, true),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", Zip, false),
        [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", Zip, false),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Zip, false),
        [".doc"] = ("application/msword", Ole, false),
        [".ppt"] = ("application/vnd.ms-powerpoint", Ole, false),
        [".xls"] = ("application/vnd.ms-excel", Ole, false),
        [".png"] = ("image/png", Png, true),
        [".jpg"] = ("image/jpeg", Jpeg, true),
        [".jpeg"] = ("image/jpeg", Jpeg, true),
    };

    /// <summary>Kabul edilen uzantılar (dosya seçicide ve hata iletisinde).</summary>
    public static IReadOnlyCollection<string> Extensions => Types.Keys;

    /// <summary>Uzantı ve içerik uyuşursa türü; desteklenmiyorsa null.</summary>
    public static DocumentFileType? Detect(string fileName, ReadOnlySpan<byte> header)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!Types.TryGetValue(extension, out var type) || !header.StartsWith(type.Magic))
            return null;
        return new DocumentFileType(extension == ".jpeg" ? ".jpg" : extension, type.ContentType, type.Inline);
    }

    public static bool IsInline(string contentType) => contentType is "application/pdf" or "image/png" or "image/jpeg";

    /// <summary>Aranacak metin; okunamayan (bozuk, parola korumalı, eski Office biçimi, resim) belgede boş metin.</summary>
    public static DocumentText ExtractText(string extension, Stream content)
    {
        try
        {
            switch (extension)
            {
                case ".pdf":
                    var pages = PdfTextExtractor.ExtractPages(content);
                    return new DocumentText(string.Join("\n\n", pages.Where(p => p.Length > 0)), pages.Count);
                case ".docx":
                    return new DocumentText(DocxTextExtractor.Extract(content), null);
                case ".pptx":
                    return new DocumentText(Pptx(content), null);
                case ".xlsx":
                    return new DocumentText(Xlsx(content), null);
                default:
                    return new DocumentText("", null);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or UglyToad.PdfPig.Core.PdfDocumentFormatException
            or InvalidOperationException or IOException)
        {
            return new DocumentText("", null);
        }
    }

    /// <summary>Sunum: slaytlar sırasıyla, her paragraf bir satır (ppt/slides/slideN.xml).</summary>
    private static string Pptx(Stream content)
    {
        using var archive = new ZipArchive(content, ZipArchiveMode.Read);
        var slides = archive.Entries
            .Select(e => (Entry: e, Match: SlideName().Match(e.FullName)))
            .Where(x => x.Match.Success)
            .OrderBy(x => int.Parse(x.Match.Groups[1].Value))
            .Select(x => x.Entry);
        var text = new StringBuilder();
        foreach (var slide in slides)
        {
            var lines = ReadText(slide, "http://schemas.openxmlformats.org/drawingml/2006/main", "t", "p");
            if (lines.Length > 0)
                text.Append(lines).Append("\n\n");
        }
        return text.ToString().Trim();
    }

    /// <summary>Tablo: hücrelerdeki metinler (paylaşılan dizgiler); sayılar aranmaz.</summary>
    private static string Xlsx(Stream content)
    {
        using var archive = new ZipArchive(content, ZipArchiveMode.Read);
        return archive.GetEntry("xl/sharedStrings.xml") is { } strings
            ? ReadText(strings, "http://schemas.openxmlformats.org/spreadsheetml/2006/main", "t", "si").Trim()
            : "";
    }

    /// <summary><paramref name="textElement"/> metinlerini, her <paramref name="lineElement"/> sonunda satır atlayarak birleştirir.</summary>
    private static string ReadText(ZipArchiveEntry entry, string ns, string textElement, string lineElement)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var text = new StringBuilder();
        var line = new StringBuilder();
        reader.Read();
        while (!reader.EOF)
        {
            // ReadElementContentAsString okuyucuyu bir sonraki düğüme taşır; o düğüm atlanmasın diye Read çağrılmaz.
            if (reader.NamespaceURI == ns && reader.NodeType == XmlNodeType.Element && reader.LocalName == textElement)
            {
                line.Append(reader.ReadElementContentAsString());
                continue;
            }
            if (reader.NamespaceURI == ns && reader.NodeType == XmlNodeType.EndElement && reader.LocalName == lineElement)
            {
                if (line.Length > 0)
                    text.Append(line.ToString().Trim()).Append('\n');
                line.Clear();
            }
            reader.Read();
        }
        text.Append(line);
        return text.ToString().TrimEnd();
    }

    [GeneratedRegex(@"^ppt/slides/slide(\d+)\.xml$")]
    private static partial Regex SlideName();
}
