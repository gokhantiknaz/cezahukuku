using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CezaHukuku.IntegrationTests;

/// <summary>Sona bırakılan içe aktarma işleri: dipnotlar, dosyadan metin, eski kanun karşılıkları tablosu.</summary>
[Collection(ApiCollection.Name)]
public class BulkImportTests(ApiFactory factory)
{
    private static readonly string TckExcerpt =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tck-kesit.txt"));

    [Fact]
    public async Task Import_AddsFootnotesAsArticleNote_AndUpdatesItOnReimport()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, "5237", "TCK");
        var request = new ImportLawTextRequest(TckExcerpt, false, new DateOnly(2005, 6, 1), false);

        var preview = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import/preview", request)).ReadAsync<LawImportPreview>();
        var m82 = preview.Nodes.SelectMany(AllArticles).Single(a => a.Number == "82");
        Assert.Equal(3, m82.Footnotes.Count);
        Assert.StartsWith("[33] 29/6/2005", m82.Footnotes[0]);

        await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import", request).ExpectAsync(200);
        var note = Assert.Single(await NotesAsync(admin, law, "madde-82"));
        Assert.Equal((EditorialNoteKind.Aciklama, "Mevzuat dipnotları"), (note.Kind, note.Title));
        Assert.Contains("<p>[34] 8/7/2021 tarihli ve 7331 sayılı Kanunun", note.Html);
        Assert.Empty(await NotesAsync(admin, law, "madde-81"));

        // Dipnot metni değişince aynı not güncellenir (ikinci not eklenmez).
        var changed = TckExcerpt.Replace("[34] 8/7/2021 tarihli", "[34] 9/7/2021 tarihli");
        await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import", request with { Text = changed }).ExpectAsync(200);
        var updated = Assert.Single(await NotesAsync(admin, law, "madde-82"));
        Assert.Equal(note.Id, updated.Id);
        Assert.Contains("[34] 9/7/2021 tarihli", updated.Html);
    }

    [Fact]
    public async Task Import_WithoutFootnotes_WhenDisabled()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, "5237", "TCK");
        await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import",
            new ImportLawTextRequest(TckExcerpt, false, new DateOnly(2005, 6, 1), false, ImportFootnotes: false)).ExpectAsync(200);

        Assert.Empty(await NotesAsync(admin, law, "madde-82"));
    }

    [Fact]
    public async Task ExtractText_ReadsDocxPdfAndText_AndRejectsOldWord()
    {
        var admin = await factory.CreateAdminClientAsync();

        var docx = await ExtractAsync(admin, BuildDocx("Kasten öldürme", "Madde 81- (1) Bir insanı kasten öldüren kişi"), "kanun.docx");
        Assert.Equal(("Kasten öldürme\nMadde 81- (1) Bir insanı kasten öldüren kişi", false), (docx.Text, docx.JoinWrappedLines));

        var pdf = await ExtractAsync(admin, BuildPdf("Madde 81- (1) Bir insani kasten olduren kisi"), "kanun.pdf");
        Assert.True(pdf.JoinWrappedLines);
        Assert.Contains("Madde 81- (1) Bir insani kasten olduren kisi", pdf.Text);

        var text = await ExtractAsync(admin, Encoding.UTF8.GetBytes("Madde 1- (1) Düz metin."), "kanun.txt");
        Assert.Equal("Madde 1- (1) Düz metin.", text.Text);

        await admin.PostAsync("/api/admin/import/extract-text", Upload([1, 2, 3], "kanun.doc")).ExpectAsync(400);
        await admin.PostAsync("/api/admin/import/extract-text", Upload([1, 2, 3], "kanun.docx")).ExpectAsync(400);
        await factory.CreateAnonymousClient().PostAsync("/api/admin/import/extract-text", Upload([1], "a.txt")).ExpectAsync(401);
    }

    [Fact]
    public async Task OldLawLinks_BulkFromTable_LinksExistingCreatesLabelsAndSkipsDuplicates()
    {
        var admin = await factory.CreateAdminClientAsync();
        var tck = await CreateLawAsync(admin, "5237", "TCK", "81", "82", "83");
        var old = await CreateLawAsync(admin, "765", "ETCK", "448", "449", "450");
        List<OldLawLinkRow> rows =
        [
            new("81", "448", "Temel şekil"),
            new("82", "449, 450", null),
            new("83", "999", null),
            new("84", "448", null),
        ];

        var dry = await (await admin.PostJsonAsync($"/api/admin/laws/{tck.Id}/old-law-links/bulk",
            new BulkOldLawLinksRequest(old.Id, null, rows, DryRun: true))).ReadAsync<BulkOldLawLinksResult>();
        Assert.Equal((3, 0, 2), (dry.Created, dry.Exists, dry.Errors));
        Assert.Empty((await ArticleAsync(admin, tck, "madde-81")).Links);

        var saved = await (await admin.PostJsonAsync($"/api/admin/laws/{tck.Id}/old-law-links/bulk",
            new BulkOldLawLinksRequest(old.Id, null, rows, DryRun: false))).ReadAsync<BulkOldLawLinksResult>();
        Assert.Equal(3, saved.Created);
        Assert.Contains(saved.Rows, r => r is { Row: 3, Status: BulkRowStatus.Error } && r.Message!.Contains("999"));
        Assert.Contains(saved.Rows, r => r is { Row: 4, Status: BulkRowStatus.Error } && r.Message!.Contains("84"));

        var m81 = (await ArticleAsync(admin, tck, "madde-81")).Links.Single();
        Assert.Equal((ArticleLinkKind.EskiKanunKarsiligi, "448", "Temel şekil"), (m81.Kind, m81.Article!.Number, m81.Note));
        Assert.Equal(["449", "450"], (await ArticleAsync(admin, tck, "madde-82")).Links.Select(l => l.Article!.Number));
        // Eski kanundaki maddede yeni karşılığı görünür.
        Assert.Contains((await ArticleAsync(admin, old, "madde-448")).Links, l => l.Incoming && l.Article!.Number == "81");

        var again = await (await admin.PostJsonAsync($"/api/admin/laws/{tck.Id}/old-law-links/bulk",
            new BulkOldLawLinksRequest(old.Id, null, rows[..2], DryRun: false))).ReadAsync<BulkOldLawLinksResult>();
        Assert.Equal((0, 3), (again.Created, again.Exists));

        // Eski kanun kütüphanede değilse etiketle.
        var label = await (await admin.PostJsonAsync($"/api/admin/laws/{tck.Id}/old-law-links/bulk",
            new BulkOldLawLinksRequest(null, "765 s. TCK", [new("83", "451", null)], DryRun: false))).ReadAsync<BulkOldLawLinksResult>();
        Assert.Equal(1, label.Created);
        Assert.Equal("765 s. TCK m. 451", (await ArticleAsync(admin, tck, "madde-83")).Links.Single().ExternalLabel);

        await admin.PostJsonAsync($"/api/admin/laws/{tck.Id}/old-law-links/bulk",
            new BulkOldLawLinksRequest(null, null, rows, DryRun: true)).ExpectAsync(400);
    }

    private static IEnumerable<ImportArticlePreview> AllArticles(ImportNodePreview node) =>
        node.Articles.Concat(node.Children.SelectMany(AllArticles));

    private static async Task<List<EditorialNoteDto>> NotesAsync(HttpClient client, LawSummary law, string articleSlug) =>
        (await ArticleAsync(client, law, articleSlug)).Notes;

    private static async Task<ArticleDetailResponse> ArticleAsync(HttpClient client, LawSummary law, string articleSlug) =>
        await (await client.GetAsync($"/api/library/laws/{law.Slug}/articles/{articleSlug}")).ReadAsync<ArticleDetailResponse>();

    private static async Task<ExtractedImportText> ExtractAsync(HttpClient admin, byte[] bytes, string fileName) =>
        await (await admin.PostAsync("/api/admin/import/extract-text", Upload(bytes, fileName))).ReadAsync<ExtractedImportText>();

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    /// <summary>En küçük .docx: yalnız word/document.xml (her satır bir paragraf).</summary>
    private static byte[] BuildDocx(params string[] paragraphs)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var body = string.Concat(paragraphs.Select(p => $"<w:p><w:r><w:t>{System.Security.SecurityElement.Escape(p)}</w:t></w:r></w:p>"));
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open(), new UTF8Encoding(false));
            writer.Write($"""<?xml version="1.0" encoding="UTF-8"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}</w:body></w:document>""");
        }
        return buffer.ToArray();
    }

    private static byte[] BuildPdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText(text, 11, new UglyToad.PdfPig.Core.PdfPoint(40, 750), font);
        return builder.Build();
    }

    private static async Task<LawSummary> CreateLawAsync(HttpClient admin, string number, string shortName, params string[] articleNumbers)
    {
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups",
                new SaveLawGroupRequest("Toplu", "t" + Guid.NewGuid().ToString("N")[..12], null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, number, "Deneme", shortName, "t" + Guid.NewGuid().ToString("N")[..12],
                    null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
        foreach (var article in articleNumbers)
        {
            await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, article, $"Madde {article}", Visibility.Public, null)).ExpectAsync(201);
        }
        return law;
    }
}
