using System.Net.Http.Headers;
using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Personal;
using CezaHukuku.Api.Search;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Domain.Users;
using CezaHukuku.Infrastructure.Search;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AttachmentTests(ApiFactory factory)
{
    [Fact]
    public async Task Pdf_IsProcessedPageByPage_SearchableByOwner_WithCitationsLinked()
    {
        var (admin, law, abbreviation) = await CreateLawAsync();
        var user = await CreateUserClientAsync(admin, Roles.Standard);
        var pdf = BuildPdf(
            $"Yargitay karari: sanik hakkinda {abbreviation} m. 81 uygulandi.",
            "",
            $"Ikinci sayfa: {abbreviation} 999 ve {abbreviation} 82 tartisildi; zincirleme.");

        var created = await (await user.PostAsync("/api/me/attachments", Upload(pdf, "karar.pdf", "application/pdf", "Örnek karar")))
            .ReadAsync<AttachmentDto>(201);
        Assert.Equal((AttachmentKind.Pdf, "Örnek karar", AttachmentStatus.Pending), (created.Kind, created.Title, created.Status));

        var detail = await WaitReadyAsync(user, created.Id);
        Assert.Equal((3, 1), (detail.Attachment.PageCount, detail.Attachment.ScannedPageCount));
        Assert.Equal([1, 3], detail.Segments.Select(s => s.Page));
        var citation = Assert.Single(detail.Segments[0].Citations);
        Assert.Equal(("81", law.Slug), (citation.Target.Number, citation.Target.LawSlug));
        Assert.Equal(["82"], detail.Segments[1].Citations.Select(c => c.Target.Number)); // 999 kütüphanede yok

        var hits = (await (await user.GetAsync("/api/search?q=zincirleme&mine=true")).ReadAsync<SearchResponse>()).Hits;
        var hit = Assert.Single(hits, h => h.Url.Contains(created.Id.ToString()));
        Assert.Equal((SearchKind.Document, "Sayfa 3", $"/belgelerim/{created.Id}?sayfa=3"), (hit.Kind, hit.Context, hit.Url));
        Assert.DoesNotContain((await (await admin.GetAsync("/api/search?q=zincirleme")).ReadAsync<SearchResponse>()).Hits,
            h => h.Url.Contains(created.Id.ToString()));

        var file = await user.GetAsync($"/api/me/attachments/{created.Id}/file");
        Assert.Equal("application/pdf", file.Content.Headers.ContentType!.MediaType);
        Assert.Equal(pdf, await file.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task VoiceNote_WithoutTranscriptionService_IsReady_AndTranscriptCanBeWritten()
    {
        var (admin, law, _) = await CreateLawAsync();
        var user = await CreateUserClientAsync(admin, Roles.Standard);
        var article = await (await user.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81")).ReadAsync<ArticleDetailResponse>();

        var created = await (await user.PostAsync("/api/me/attachments",
                Upload([0x1A, 0x45, 0xDF, 0xA3, 1, 2, 3], "not.webm", "audio/webm;codecs=opus", null, article.Article.Id)))
            .ReadAsync<AttachmentDto>(201);
        Assert.Equal(("audio/webm", "81"), (created.ContentType, created.Article!.Number));

        var detail = await WaitReadyAsync(user, created.Id);
        Assert.False(detail.TranscriptionEnabled);
        Assert.False(detail.Attachment.HasTranscript);

        var updated = await (await user.PutJsonAsync($"/api/me/attachments/{created.Id}",
                new UpdateAttachmentRequest("Duruşma notu", "Tanık beyanı çelişkili, olası kast tartışılmalı.")))
            .ReadAsync<AttachmentDto>();
        Assert.Equal(("Duruşma notu", true), (updated.Title, updated.HasTranscript));

        var listed = await (await user.GetAsync($"/api/me/attachments?articleId={article.Article.Id}&kind=audio")).ReadAsync<List<AttachmentDto>>();
        Assert.Equal([created.Id], listed.Select(a => a.Id));
        var hit = Assert.Single((await (await user.GetAsync($"/api/search?q=tanık beyanı&mine=true&laws={law.Slug}")).ReadAsync<SearchResponse>()).Hits);
        Assert.Equal((SearchKind.VoiceNote, $"/{law.Slug}/madde-81"), (hit.Kind, hit.Url));
    }

    [Fact]
    public async Task Attachments_ArePrivate_ValidatedAndDeletable()
    {
        var (admin, _, _) = await CreateLawAsync();
        var owner = await CreateUserClientAsync(admin, Roles.Standard);
        var other = await CreateUserClientAsync(admin, Roles.Standard);
        var reader = await CreateUserClientAsync(admin, Roles.Reader);
        var pdf = BuildPdf("Gizli belge.");

        await owner.PostAsync("/api/me/attachments", Upload("merhaba"u8.ToArray(), "a.txt", "text/plain", null)).ExpectAsync(400);
        await owner.PostAsync("/api/me/attachments", Upload("sahte pdf"u8.ToArray(), "a.pdf", "application/pdf", null)).ExpectAsync(400);
        await reader.PostAsync("/api/me/attachments", Upload(pdf, "a.pdf", "application/pdf", null)).ExpectAsync(403);

        var created = await (await owner.PostAsync("/api/me/attachments", Upload(pdf, "a.pdf", "application/pdf", null))).ReadAsync<AttachmentDto>(201);
        await other.GetAsync($"/api/me/attachments/{created.Id}").ExpectAsync(404);
        await other.GetAsync($"/api/me/attachments/{created.Id}/file").ExpectAsync(404);
        await other.DeleteAsync($"/api/me/attachments/{created.Id}").ExpectAsync(404);
        Assert.DoesNotContain(await (await other.GetAsync("/api/me/attachments")).ReadAsync<List<AttachmentDto>>(), a => a.Id == created.Id);

        await WaitReadyAsync(owner, created.Id);
        await owner.DeleteAsync($"/api/me/attachments/{created.Id}").ExpectAsync(204);
        await owner.GetAsync($"/api/me/attachments/{created.Id}").ExpectAsync(404);
        Assert.DoesNotContain((await (await owner.GetAsync("/api/search?q=gizli belge&mine=true")).ReadAsync<SearchResponse>()).Hits,
            h => h.Url.Contains(created.Id.ToString()));
    }

    private static async Task<AttachmentDetail> WaitReadyAsync(HttpClient client, Guid id)
    {
        for (var i = 0; i < 100; i++)
        {
            var detail = await (await client.GetAsync($"/api/me/attachments/{id}")).ReadAsync<AttachmentDetail>();
            if (detail.Attachment.Status != AttachmentStatus.Pending)
            {
                Assert.Equal(AttachmentStatus.Ready, detail.Attachment.Status);
                return detail;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("Ek 10 saniyede işlenmedi.");
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName, string contentType, string? title, Guid? articleId = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);
        if (title is not null)
            form.Add(new StringContent(title), "title");
        if (articleId is not null)
            form.Add(new StringContent(articleId.Value.ToString()), "articleId");
        return form;
    }

    /// <summary>Her metin bir sayfa; boş metin taranmış (metin katmanı olmayan) sayfa gibi davranır.</summary>
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

    /// <summary>Kanun kısaltması test başına benzersiz (belge atıfları kısaltmayla çözülür).</summary>
    private async Task<(HttpClient Admin, LawSummary Law, string Abbreviation)> CreateLawAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        var abbreviation = "Q" + new string([.. Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(4).Select(char.ToUpperInvariant)]).PadRight(4, 'X');
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Ekler", Slug(), null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", abbreviation, Slug(), null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
        foreach (var number in new[] { "81", "82" })
        {
            await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, number, $"Madde {number}", Visibility.Public, null)).ExpectAsync(201);
        }
        return (admin, law, abbreviation);
    }

    private async Task<HttpClient> CreateUserClientAsync(HttpClient admin, string role)
    {
        var email = $"{Slug()}@test.local";
        const string password = "TestKullanici123";
        await admin.PostJsonAsync("/api/admin/users", new CreateUserRequest(email, "Test Kullanıcı", role, password))
            .ExpectAsync(201);
        return await factory.CreateSignedInClientAsync(email, password);
    }

    private static string Slug() => "t" + Guid.NewGuid().ToString("N")[..12];
}
