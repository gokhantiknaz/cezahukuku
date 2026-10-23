using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class NoteTests(ApiFactory factory)
{
    [Fact]
    public async Task ArticleNotes_AreSanitized_AndShownWithArticle()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, article) = await CreateLawWithArticleAsync(admin);

        var note = await (await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/notes",
                new SaveEditorialNoteRequest(EditorialNoteKind.Gerekce, "Madde gerekçesi",
                    "<p>Kasten öldürme <strong>temel</strong> şekli.<script>alert(1)</script></p>", Visibility.Public, null)))
            .ReadAsync<EditorialNoteDto>(201);
        Assert.Equal("<p>Kasten öldürme <strong>temel</strong> şekli.</p>", note.Html);

        var detail = await (await factory.CreateAnonymousClient().GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81"))
            .ReadAsync<ArticleDetailResponse>();
        var shown = Assert.Single(detail.Notes);
        Assert.Equal((EditorialNoteKind.Gerekce, "Madde gerekçesi"), (shown.Kind, shown.Title));
    }

    [Fact]
    public async Task PrivateNote_AndNoteOfPrivateArticle_AreHiddenFromAnonymous()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, article) = await CreateLawWithArticleAsync(admin);
        await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/notes",
            new SaveEditorialNoteRequest(EditorialNoteKind.Aciklama, null, "<p>Herkese açık.</p>", Visibility.Public, null)).ExpectAsync(201);
        await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/notes",
            new SaveEditorialNoteRequest(EditorialNoteKind.DersNotu, null, "<p>Yalnız üyelere.</p>", Visibility.Private, null)).ExpectAsync(201);

        var url = $"/api/library/laws/{law.Slug}/articles/madde-81";
        var anonymous = await (await factory.CreateAnonymousClient().GetAsync(url)).ReadAsync<ArticleDetailResponse>();
        Assert.Equal([EditorialNoteKind.Aciklama], anonymous.Notes.Select(n => n.Kind));
        var signedIn = await (await admin.GetAsync(url)).ReadAsync<ArticleDetailResponse>();
        Assert.Equal(2, signedIn.Notes.Count);

        // Madde private olunca public notu da private sayılır.
        await admin.PutJsonAsync($"/api/admin/articles/{article.Id}",
            new SaveArticleRequest(article.NodeId, "81", "Kasten öldürme", Visibility.Private, null)).ExpectAsync(200);
        var afterPrivate = await (await admin.GetAsync(url)).ReadAsync<ArticleDetailResponse>();
        Assert.All(afterPrivate.Notes, n => Assert.Equal(Visibility.Private, n.EffectiveVisibility));
    }

    [Fact]
    public async Task LawNotes_ListedInTree_AndReadable()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, _) = await CreateLawWithArticleAsync(admin);
        await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/notes",
            new SaveEditorialNoteRequest(EditorialNoteKind.GenelGerekce, null, "<p>Genel gerekçe.</p>", Visibility.Public, null)).ExpectAsync(201);

        var tree = await (await factory.CreateAnonymousClient().GetAsync($"/api/library/laws/{law.Slug}")).ReadAsync<LawTreeResponse>();
        Assert.Equal([EditorialNoteKind.GenelGerekce], tree.NoteKinds);

        var notes = await (await factory.CreateAnonymousClient().GetAsync($"/api/library/laws/{law.Slug}/notes?kind=genelGerekce"))
            .ReadAsync<List<EditorialNoteDto>>();
        Assert.Equal("<p>Genel gerekçe.</p>", Assert.Single(notes).Html);
    }

    [Fact]
    public async Task NoteKind_MustMatchLevel_AndTextIsRequired()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, article) = await CreateLawWithArticleAsync(admin);

        await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/notes",
            new SaveEditorialNoteRequest(EditorialNoteKind.Gerekce, null, "<p>x</p>", Visibility.Public, null)).ExpectAsync(400);
        await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/notes",
            new SaveEditorialNoteRequest(EditorialNoteKind.GenelGerekce, null, "<p>x</p>", Visibility.Public, null)).ExpectAsync(400);
        await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/notes",
            new SaveEditorialNoteRequest(EditorialNoteKind.Gerekce, null, "<p><script>x</script></p>", Visibility.Public, null)).ExpectAsync(400);
    }

    [Fact]
    public async Task Note_CanBeUpdatedAndDeleted()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, article) = await CreateLawWithArticleAsync(admin);
        var note = await (await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/notes",
                new SaveEditorialNoteRequest(EditorialNoteKind.Gerekce, null, "<p>İlk.</p>", Visibility.Public, null)))
            .ReadAsync<EditorialNoteDto>(201);

        var updated = await (await admin.PutJsonAsync($"/api/admin/notes/{note.Id}",
                new SaveEditorialNoteRequest(EditorialNoteKind.Gerekce, "Başlık", "<p>Düzeltilmiş.</p>", Visibility.Public, null)))
            .ReadAsync<EditorialNoteDto>();
        Assert.Equal(("Başlık", "<p>Düzeltilmiş.</p>"), (updated.Title, updated.Html));

        await admin.DeleteAsync($"/api/admin/notes/{note.Id}").ExpectAsync(204);
        var detail = await (await admin.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81")).ReadAsync<ArticleDetailResponse>();
        Assert.Empty(detail.Notes);
    }

    private static async Task<(LawSummary Law, ArticleSummary Article)> CreateLawWithArticleAsync(HttpClient admin)
    {
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups",
                new SaveLawGroupRequest("Notlar", "t" + Guid.NewGuid().ToString("N")[..12], null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", "t" + Guid.NewGuid().ToString("N")[..12],
                    null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
        var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, "81", "Kasten öldürme", Visibility.Public, null)))
            .ReadAsync<ArticleSummary>(201);
        return (law, article);
    }
}
