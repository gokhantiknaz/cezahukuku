using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Content;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Search;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Users;
using CezaHukuku.Infrastructure.Search;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class LectureNoteTests(ApiFactory factory)
{
    [Fact]
    public async Task LectureNotes_AreGeneral_OrLinkedToGroupLawOrArticle()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (group, law, article) = await CreateLawWithArticleAsync(admin);
        var title = $"Kast ve taksir {Guid.NewGuid():N}"[..30];

        var general = await CreateAsync(admin, new SaveLectureNoteRequest(title, "<p>Genel anlatım.</p><script>x</script>", Visibility.Public, null, null, null));
        var again = await CreateAsync(admin, new SaveLectureNoteRequest(title, "<p>Aynı başlık.</p>", Visibility.Public, null, null, null));
        var forGroup = await CreateAsync(admin, new SaveLectureNoteRequest("Grup notu", "<p>Grup.</p>", Visibility.Public, group.Id, null, null));
        // Yalnız madde verilir: kanunu ve grubu maddeden gelir.
        var forArticle = await CreateAsync(admin, new SaveLectureNoteRequest("Madde notu", "<p>Madde.</p>", Visibility.Public, null, null, article.Id));

        Assert.Equal("<p>Genel anlatım.</p>", general.Html);
        Assert.Equal((null, null, null), (general.Scope.Group, general.Scope.Law, general.Scope.Article));
        Assert.Equal(general.Slug + "-2", again.Slug);
        Assert.Equal((group.Id, null), (forGroup.Scope.Group?.Id, forGroup.Scope.Law));
        Assert.Equal((group.Id, law.Id, article.Id), (forArticle.LawGroupId, forArticle.LawId, forArticle.ArticleId));
        Assert.Equal(("TCK", "81"), (forArticle.Scope.Law?.Label, forArticle.Scope.Article?.Number));

        var anonymous = factory.CreateAnonymousClient();
        var detail = await (await anonymous.GetAsync($"/api/lecture-notes/{forArticle.Slug}")).ReadAsync<LectureNoteDetail>();
        Assert.Equal("Madde notu", detail.Title);
        var list = await (await anonymous.GetAsync("/api/lecture-notes")).ReadAsync<List<LectureNoteSummary>>();
        Assert.Contains(list, n => n.Slug == general.Slug && n.Excerpt == "Genel anlatım.");

        // Madde ekranında bağlantı; aramada ders notu olarak, kendi sayfasının adresiyle.
        var articleDetail = await (await anonymous.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81")).ReadAsync<ArticleDetailResponse>();
        Assert.Equal([new LectureNoteRef(forArticle.Slug, "Madde notu")], articleDetail.LectureNotes);
        var search = await (await anonymous.GetAsync($"/api/search?q={Uri.EscapeDataString(title)}&kinds=dersNotu")).ReadAsync<SearchResponse>();
        Assert.Contains(search.Hits, h => h.Kind == SearchKind.DersNotu && h.Url == $"/ders-notlari/{general.Slug}" && h.Context == "Genel");
    }

    [Fact]
    public async Task PrivateNote_IsHiddenFromAnonymous_AndOnlyAdminWrites()
    {
        var admin = await factory.CreateAdminClientAsync();
        var note = await CreateAsync(admin, new SaveLectureNoteRequest("Üyelere özel", "<p>Gizli.</p>", Visibility.Private, null, null, null));

        var anonymous = factory.CreateAnonymousClient();
        await anonymous.GetAsync($"/api/lecture-notes/{note.Slug}").ExpectAsync(404);
        Assert.DoesNotContain(await (await anonymous.GetAsync("/api/lecture-notes")).ReadAsync<List<LectureNoteSummary>>(), n => n.Id == note.Id);
        await admin.GetAsync($"/api/lecture-notes/{note.Slug}").ExpectAsync(200);

        var standard = await CreateUserClientAsync(admin, Roles.Standard);
        await standard.GetAsync($"/api/lecture-notes/{note.Slug}").ExpectAsync(200);
        await standard.PostJsonAsync("/api/admin/lecture-notes", new SaveLectureNoteRequest("X", "<p>x</p>", Visibility.Public, null, null, null)).ExpectAsync(403);
        await anonymous.PutJsonAsync($"/api/admin/lecture-notes/{note.Id}", new SaveLectureNoteRequest("X", "<p>x</p>", Visibility.Public, null, null, null)).ExpectAsync(401);

        // Güncellemede adres değişmez; silinen not açılmaz.
        var updated = await (await admin.PutJsonAsync($"/api/admin/lecture-notes/{note.Id}",
            new SaveLectureNoteRequest("Yeni başlık", "<p>Gizli.</p>", Visibility.Public, null, null, null))).ReadAsync<LectureNoteDetail>();
        Assert.Equal((note.Slug, "Yeni başlık"), (updated.Slug, updated.Title));
        await admin.DeleteAsync($"/api/admin/lecture-notes/{note.Id}").ExpectAsync(204);
        await admin.GetAsync($"/api/lecture-notes/{note.Slug}").ExpectAsync(404);
    }

    [Fact]
    public async Task Validation_AndOldArticleLevelLectureNotesAreRejected()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (_, _, article) = await CreateLawWithArticleAsync(admin);

        await admin.PostJsonAsync("/api/admin/lecture-notes", new SaveLectureNoteRequest(" ", "<p>x</p>", Visibility.Public, null, null, null)).ExpectAsync(400);
        // Metin boş olabilir (ders notu yalnız belgelerden oluşabilir); temizlenen betik kalmaz.
        var empty = await CreateAsync(admin, new SaveLectureNoteRequest("Başlık", "<script>x</script>", Visibility.Public, null, null, null));
        Assert.Equal("", empty.Html);
        await admin.PostJsonAsync("/api/admin/lecture-notes", new SaveLectureNoteRequest("Başlık", "<p>x</p>", Visibility.Public, null, Guid.NewGuid(), null)).ExpectAsync(400);
        // Ders notu artık madde/kanun notu olarak girilmez.
        await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/notes",
            new SaveEditorialNoteRequest(EditorialNoteKind.DersNotu, null, "<p>x</p>", Visibility.Public, null)).ExpectAsync(400);
    }

    private static async Task<LectureNoteDetail> CreateAsync(HttpClient admin, SaveLectureNoteRequest request) =>
        await (await admin.PostJsonAsync("/api/admin/lecture-notes", request)).ReadAsync<LectureNoteDetail>(201);

    private static async Task<(LawGroupSummary Group, LawSummary Law, ArticleSummary Article)> CreateLawWithArticleAsync(HttpClient admin)
    {
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Temel Ceza Kanunları", Slug(), null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", Slug(), null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
        var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, "81", "Kasten öldürme", Visibility.Public, null)))
            .ReadAsync<ArticleSummary>(201);
        return (group, law, article);
    }

    private async Task<HttpClient> CreateUserClientAsync(HttpClient admin, string role)
    {
        var email = $"{Slug()}@test.local";
        const string password = "TestKullanici123";
        await admin.PostJsonAsync("/api/admin/users", new CreateUserRequest(email, "Test Kullanıcı", role, password)).ExpectAsync(201);
        return await factory.CreateSignedInClientAsync(email, password);
    }

    private static string Slug() => "t" + Guid.NewGuid().ToString("N")[..12];
}
