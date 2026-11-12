using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Content;
using CezaHukuku.Api.Search;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Users;
using CezaHukuku.Infrastructure.Search;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class LectureNoteFileTests(ApiFactory factory)
{
    [Fact]
    public async Task Files_AreAttached_Listed_Served_AndSearchable()
    {
        var admin = await factory.CreateAdminClientAsync();
        var word = "zq" + Guid.NewGuid().ToString("N")[..8];
        var note = await CreateNoteAsync(admin, Visibility.Public);

        var pdf = await UploadAsync(admin, note.Id, BuildPdf($"Birinci sayfa {word}.", ""), "Kast sunumu.pdf", "Kast — slaytlar");
        Assert.Equal(("Kast — slaytlar", "application/pdf", 2, true), (pdf.Title, pdf.ContentType, pdf.PageCount, pdf.HasText));
        var docx = await UploadAsync(admin, note.Id, BuildDocx("Word belgesi", "ikinci paragraf"), "okuma-listesi.docx", null);
        Assert.Equal(("okuma-listesi", (int?)null, true), (docx.Title, docx.PageCount, docx.HasText));
        var pptx = await UploadAsync(admin, note.Id, BuildPptx("Slayt bir", "Slayt iki"), "ders.pptx", null);
        var png = await UploadAsync(admin, note.Id, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0], "sema.png", null);
        Assert.False(png.HasText);

        // Uzantı ve içerik uyuşmalı; betik çalıştırabilecek türler kabul edilmez.
        await admin.PostAsync($"/api/admin/lecture-notes/{note.Id}/files", Form("sahte pdf"u8.ToArray(), "a.pdf", null)).ExpectAsync(400);
        await admin.PostAsync($"/api/admin/lecture-notes/{note.Id}/files", Form("<html></html>"u8.ToArray(), "a.html", null)).ExpectAsync(400);
        await admin.PostAsync($"/api/admin/lecture-notes/{note.Id}/files", Form(BuildPdf("x"), "a.docx", null)).ExpectAsync(400);
        await admin.PostAsync($"/api/admin/lecture-notes/{Guid.NewGuid()}/files", Form(BuildPdf("x"), "a.pdf", null)).ExpectAsync(404);

        // Notun sayfasında eklenme sırasıyla; listede sayısı.
        var anonymous = factory.CreateAnonymousClient();
        var detail = await (await anonymous.GetAsync($"/api/lecture-notes/{note.Slug}")).ReadAsync<LectureNoteDetail>();
        Assert.Equal([pdf.Id, docx.Id, pptx.Id, png.Id], detail.Files.Select(f => f.Id));
        var summary = (await (await anonymous.GetAsync("/api/lecture-notes")).ReadAsync<List<LectureNoteSummary>>()).Single(n => n.Id == note.Id);
        Assert.Equal(4, summary.FileCount);

        // PDF tarayıcıda açılır (adı korunur), Word iner.
        var pdfResponse = await anonymous.GetAsync(pdf.Url);
        Assert.Equal(200, (int)pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", pdfResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("Kast sunumu.pdf", pdfResponse.Content.Headers.ContentDisposition?.FileNameStar);
        var docxResponse = await anonymous.GetAsync(docx.Url);
        Assert.Equal("attachment", docxResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(BuildDocx("Word belgesi", "ikinci paragraf").Length, (await docxResponse.Content.ReadAsByteArrayAsync()).Length);
        var forced = await anonymous.GetAsync(pdf.Url + "?download=true");
        Assert.Equal("attachment", forced.Content.Headers.ContentDisposition?.DispositionType);
        // Başka notun adresiyle açılmaz.
        var other = await CreateNoteAsync(admin, Visibility.Public);
        await anonymous.GetAsync($"/api/lecture-notes/{other.Slug}/files/{pdf.Id}").ExpectAsync(404);

        // Belgenin metni aranır; sonuç notun sayfasındaki belgeye gider.
        var search = await (await anonymous.GetAsync($"/api/search?q={word}&kinds=dersNotu")).ReadAsync<SearchResponse>();
        var hit = Assert.Single(search.Hits);
        Assert.Equal(($"/ders-notlari/{note.Slug}#belge-{pdf.Id}", SearchKind.DersNotu), (hit.Url, hit.Kind));
        Assert.Contains("ekli belge", hit.Context);
        Assert.Contains((await (await anonymous.GetAsync("/api/search?q=slayt iki&kinds=dersNotu")).ReadAsync<SearchResponse>()).Hits,
            h => h.Url.EndsWith(pptx.Id.ToString()));

        // Ad değiştirme, sıralama, silme.
        var renamed = await (await admin.PutJsonAsync($"/api/admin/lecture-notes/{note.Id}/files/{docx.Id}",
            new UpdateLectureNoteFileRequest("Okuma listesi"))).ReadAsync<LectureNoteFileDto>();
        Assert.Equal("Okuma listesi", renamed.Title);
        await admin.PutJsonAsync($"/api/admin/lecture-notes/{note.Id}/files/order",
            new ReorderLectureNoteFilesRequest([png.Id, pptx.Id, docx.Id, pdf.Id])).ExpectAsync(204);
        await admin.PutJsonAsync($"/api/admin/lecture-notes/{note.Id}/files/order",
            new ReorderLectureNoteFilesRequest([png.Id, pptx.Id])).ExpectAsync(400);
        await admin.PutJsonAsync($"/api/admin/lecture-notes/{note.Id}/files/order", new { }).ExpectAsync(400);
        await admin.DeleteAsync($"/api/admin/lecture-notes/{note.Id}/files/{pdf.Id}").ExpectAsync(204);
        detail = await (await anonymous.GetAsync($"/api/lecture-notes/{note.Slug}")).ReadAsync<LectureNoteDetail>();
        Assert.Equal([png.Id, pptx.Id, docx.Id], detail.Files.Select(f => f.Id));
        await anonymous.GetAsync(pdf.Url).ExpectAsync(404);
        Assert.Empty((await (await anonymous.GetAsync($"/api/search?q={word}&kinds=dersNotu")).ReadAsync<SearchResponse>()).Hits);

        // Not silinince belgeleri de açılmaz.
        await admin.DeleteAsync($"/api/admin/lecture-notes/{note.Id}").ExpectAsync(204);
        await anonymous.GetAsync(docx.Url).ExpectAsync(404);
    }

    [Fact]
    public async Task PrivateNoteFiles_AreForMembers_AndOnlyAdminUploads()
    {
        var admin = await factory.CreateAdminClientAsync();
        var word = "zq" + Guid.NewGuid().ToString("N")[..8];
        var note = await CreateNoteAsync(admin, Visibility.Private);
        var file = await UploadAsync(admin, note.Id, BuildPdf($"Gizli {word}"), "gizli.pdf", null);

        var anonymous = factory.CreateAnonymousClient();
        await anonymous.GetAsync(file.Url).ExpectAsync(404);
        Assert.Empty((await (await anonymous.GetAsync($"/api/search?q={word}")).ReadAsync<SearchResponse>()).Hits);

        var standard = await CreateUserClientAsync(admin, Roles.Standard);
        await standard.GetAsync(file.Url).ExpectAsync(200);
        Assert.NotEmpty((await (await standard.GetAsync($"/api/search?q={word}")).ReadAsync<SearchResponse>()).Hits);
        await standard.PostAsync($"/api/admin/lecture-notes/{note.Id}/files", Form(BuildPdf("x"), "a.pdf", null)).ExpectAsync(403);
        await standard.DeleteAsync($"/api/admin/lecture-notes/{note.Id}/files/{file.Id}").ExpectAsync(403);
        await anonymous.PostAsync($"/api/admin/lecture-notes/{note.Id}/files", Form(BuildPdf("x"), "a.pdf", null)).ExpectAsync(401);
    }

    private static async Task<LectureNoteDetail> CreateNoteAsync(HttpClient admin, Visibility visibility) =>
        await (await admin.PostJsonAsync("/api/admin/lecture-notes",
            new SaveLectureNoteRequest($"Belgeli not {Guid.NewGuid():N}"[..24], "", visibility, null, null, null))).ReadAsync<LectureNoteDetail>(201);

    private static async Task<LectureNoteFileDto> UploadAsync(HttpClient admin, Guid noteId, byte[] bytes, string fileName, string? title) =>
        await (await admin.PostAsync($"/api/admin/lecture-notes/{noteId}/files", Form(bytes, fileName, title))).ReadAsync<LectureNoteFileDto>(201);

    private static MultipartFormDataContent Form(byte[] bytes, string fileName, string? title)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        form.Add(file, "file", fileName);
        if (title is not null)
            form.Add(new StringContent(title), "title");
        return form;
    }

    private static byte[] BuildPdf(params string[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            if (text.Length > 0)
                page.AddText(text, 11, new UglyToad.PdfPig.Core.PdfPoint(40, 750), font);
        }
        return builder.Build();
    }

    private static byte[] BuildDocx(params string[] paragraphs) => Zip(("word/document.xml",
        "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>"
        + string.Concat(paragraphs.Select(p => $"<w:p><w:r><w:t>{p}</w:t></w:r></w:p>"))
        + "</w:body></w:document>"));

    private static byte[] BuildPptx(params string[] slides) => Zip([.. slides.Select((text, i) => ($"ppt/slides/slide{i + 1}.xml",
        "<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">"
        + $"<p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>{text}</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>"))]);

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

    private async Task<HttpClient> CreateUserClientAsync(HttpClient admin, string role)
    {
        var email = $"t{Guid.NewGuid():N}"[..13] + "@test.local";
        const string password = "TestKullanici123";
        await admin.PostJsonAsync("/api/admin/users", new CreateUserRequest(email, "Test Kullanıcı", role, password)).ExpectAsync(201);
        return await factory.CreateSignedInClientAsync(email, password);
    }
}
