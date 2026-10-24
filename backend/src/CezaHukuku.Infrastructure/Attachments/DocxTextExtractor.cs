using System.IO.Compression;
using System.Text;
using System.Xml;

namespace CezaHukuku.Infrastructure.Attachments;

/// <summary>
/// Word (.docx) belgesinin düz metni: her paragraf bir satır, sekme ve satır sonları korunur. .docx bir zip arşividir;
/// metin <c>word/document.xml</c> içindedir (ayrı bir kütüphane gerekmez). Eski .doc biçimi desteklenmez.
/// </summary>
public static class DocxTextExtractor
{
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static string Extract(Stream docx)
    {
        using var archive = new ZipArchive(docx, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("Word belgesi değil.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

        var text = new StringBuilder();
        var paragraph = new StringBuilder();
        while (reader.Read())
        {
            if (reader.NamespaceURI != WordNamespace)
                continue;
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.LocalName)
                {
                    case "t":
                        paragraph.Append(reader.ReadElementContentAsString());
                        break;
                    case "tab":
                        paragraph.Append('\t');
                        break;
                    case "br" or "cr":
                        paragraph.Append('\n');
                        break;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "p")
            {
                text.Append(paragraph).Append('\n');
                paragraph.Clear();
            }
        }
        text.Append(paragraph);
        return text.ToString().TrimEnd();
    }
}
