using System.IO.Compression;
using System.Text;
using CezaHukuku.Infrastructure.Attachments;

namespace CezaHukuku.UnitTests.Content;

public class DocumentFilesTests
{
    [Theory]
    [InlineData("ders.pdf", "%PDF-1.7", ".pdf")]
    [InlineData("DERS.PDF", "%PDF-1.7", ".pdf")]
    [InlineData("sunum.pptx", "PK\u0003\u0004", ".pptx")]
    [InlineData("foto.jpeg", "ÿØÿà", ".jpg")]
    [InlineData("sahte.pdf", "merhaba", null)]
    [InlineData("pdf.docx", "%PDF-1.7", null)]
    [InlineData("sayfa.html", "<html>", null)]
    [InlineData("resim.svg", "<svg", null)]
    public void Detect_RequiresMatchingExtensionAndContent(string fileName, string header, string? expected)
    {
        var bytes = header.Select(c => (byte)c).ToArray();
        Assert.Equal(expected, DocumentFiles.Detect(fileName, bytes)?.Extension);
    }

    [Fact]
    public void Text_IsReadFromSlidesInOrder_AndSpreadsheetCells()
    {
        // slide10 slide2'den sonra gelir (sayısal sıra).
        var pptx = Zip(
            ("ppt/slides/slide10.xml", Slide("Onuncu")),
            ("ppt/slides/slide2.xml", Slide("İkinci")),
            ("ppt/slides/_rels/slide2.xml.rels", "<Relationships/>"));
        Assert.Equal("İkinci\n\nOnuncu", DocumentFiles.ExtractText(".pptx", new MemoryStream(pptx)).Text);

        var xlsx = Zip(("xl/sharedStrings.xml",
            "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>Kast</t></si><si><t>Taksir</t></si></sst>"));
        Assert.Equal("Kast\nTaksir", DocumentFiles.ExtractText(".xlsx", new MemoryStream(xlsx)).Text);

        // Bozuk dosya: metin boş (belge yine eklenir).
        Assert.Equal("", DocumentFiles.ExtractText(".docx", new MemoryStream("PK\u0003\u0004bozuk"u8.ToArray())).Text);
    }

    private static string Slide(string text) =>
        "<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">"
        + $"<p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>{text}</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>";

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(content));
            }
        }
        return buffer.ToArray();
    }
}
