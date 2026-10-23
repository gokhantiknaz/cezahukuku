using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class LibraryTests(ApiFactory factory)
{
    private const string Text81 = "Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.";

    [Fact]
    public async Task Anonymous_ReadsPublicArticleWithBreadcrumbAndText()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, node) = await CreateLawAsync(admin);
        var article = await CreateArticleAsync(admin, law, node, "81", "Kasten öldürme");
        await CreateVersionAsync(admin, article, new DateOnly(2005, 6, 1), Text81);

        var anonymous = factory.CreateAnonymousClient();
        var detail = await (await anonymous.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81"))
            .ReadAsync<ArticleDetailResponse>();

        Assert.Equal("Kasten öldürme", detail.Article.Title);
        Assert.Equal([node.Title], detail.Path.Select(p => p.Title));
        Assert.Equal(Text81, detail.Version?.Paragraphs.Single().Text);

        var tree = await (await anonymous.GetAsync($"/api/library/laws/{law.Slug}")).ReadAsync<LawTreeResponse>();
        Assert.Equal(["81"], tree.Nodes.Single().Articles.Select(a => a.Number));
    }

    [Fact]
    public async Task PrivateLaw_IsHiddenFromAnonymousButVisibleToAnySignedInUser()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, node) = await CreateLawAsync(admin, Visibility.Private);
        await CreateArticleAsync(admin, law, node, "1", "Amaç");

        var anonymous = factory.CreateAnonymousClient();
        await anonymous.GetAsync($"/api/library/laws/{law.Slug}").ExpectAsync(404);
        await anonymous.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-1").ExpectAsync(404);
        var library = await (await anonymous.GetAsync("/api/library")).ReadAsync<LibraryResponse>();
        Assert.DoesNotContain(library.Groups.SelectMany(g => g.Laws), l => l.Id == law.Id);

        var reader = await CreateUserClientAsync(admin, "reader");
        await reader.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-1").ExpectAsync(200);
    }

    [Fact]
    public async Task PrivateNode_HidesItsArticlesFromAnonymous()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, node) = await CreateLawAsync(admin);
        await CreateArticleAsync(admin, law, node, "1", "Amaç");
        await admin.PutJsonAsync($"/api/admin/nodes/{node.Id}",
            new SaveLawNodeRequest(null, node.Kind, node.Number, node.Title, Visibility.Private, null)).ExpectAsync(200);

        var anonymous = factory.CreateAnonymousClient();
        await anonymous.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-1").ExpectAsync(404);
        var tree = await (await anonymous.GetAsync($"/api/library/laws/{law.Slug}")).ReadAsync<LawTreeResponse>();
        Assert.Empty(tree.Nodes);

        var signedIn = await (await admin.GetAsync($"/api/library/laws/{law.Slug}")).ReadAsync<LawTreeResponse>();
        Assert.Equal(Visibility.Private, signedIn.Nodes.Single().Articles.Single().EffectiveVisibility);
    }

    [Fact]
    public async Task NewVersion_ClosesPreviousOne_AndAsOfReturnsTextInForceOnThatDay()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, node) = await CreateLawAsync(admin);
        var article = await CreateArticleAsync(admin, law, node, "81", "Kasten öldürme");
        var first = await CreateVersionAsync(admin, article, new DateOnly(2005, 6, 1), "İlk metin.");
        await CreateVersionAsync(admin, article, new DateOnly(2014, 7, 2), "Değişen metin.");

        var url = $"/api/library/laws/{law.Slug}/articles/madde-81";
        var current = await (await admin.GetAsync(url)).ReadAsync<ArticleDetailResponse>();
        Assert.Equal("Değişen metin.", current.Version?.Paragraphs.Single().Text);
        Assert.Equal(new DateOnly(2014, 7, 1), current.Versions.Single(v => v.Id == first.Id).ValidTo);

        var atCrimeDate = await (await admin.GetAsync(url + "?asOf=2010-03-15")).ReadAsync<ArticleDetailResponse>();
        Assert.Equal("İlk metin.", atCrimeDate.Version?.Paragraphs.Single().Text);

        var beforeLaw = await (await admin.GetAsync(url + "?asOf=2000-01-01")).ReadAsync<ArticleDetailResponse>();
        Assert.Null(beforeLaw.Version);

        var history = await (await admin.GetAsync(url + "/history")).ReadAsync<List<ArticleVersionDetail>>();
        Assert.Equal(["Değişen metin.", "İlk metin."], history.Select(v => v.Paragraphs.Single().Text));
    }

    [Fact]
    public async Task History_OfPrivateArticle_IsHiddenFromAnonymous()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, node) = await CreateLawAsync(admin);
        var article = await CreateArticleAsync(admin, law, node, "1", "Amaç");
        await CreateVersionAsync(admin, article, new DateOnly(2005, 6, 1), "Metin.");
        await admin.PutJsonAsync($"/api/admin/articles/{article.Id}",
            new SaveArticleRequest(node.Id, "1", "Amaç", Visibility.Private, null)).ExpectAsync(200);

        var url = $"/api/library/laws/{law.Slug}/articles/madde-1/history";
        await factory.CreateAnonymousClient().GetAsync(url).ExpectAsync(404);
        await admin.GetAsync(url).ExpectAsync(200);
    }

    [Fact]
    public async Task NewVersion_StartingBeforeVersionInForce_IsRejected()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, node) = await CreateLawAsync(admin);
        var article = await CreateArticleAsync(admin, law, node, "81", "Kasten öldürme");
        await CreateVersionAsync(admin, article, new DateOnly(2014, 7, 2), "Metin.");

        await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions", VersionRequest(new DateOnly(2010, 1, 1), "Eski."))
            .ExpectAsync(400);
    }

    [Fact]
    public async Task ParagraphIds_AreKeptAcrossVersions()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, node) = await CreateLawAsync(admin);
        var article = await CreateArticleAsync(admin, law, node, "81", "Kasten öldürme");
        var first = await CreateVersionAsync(admin, article, new DateOnly(2005, 6, 1), "İlk metin.");
        var paragraphId = first.Paragraphs.Single().Id;

        var second = await (await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
                new SaveArticleVersionRequest(new DateOnly(2014, 7, 2), null, "6545 s.K. m.57", null, null, false,
                    [new ParagraphInput(paragraphId, 1, "Değişen metin.", null), new ParagraphInput(null, 2, "Yeni fıkra.", null)])))
            .ReadAsync<ArticleVersionDetail>(201);

        Assert.Equal(paragraphId, second.Paragraphs[0].Id);
        Assert.NotEqual(paragraphId, second.Paragraphs[1].Id);
    }

    [Fact]
    public async Task ParseText_KeepsIdsOfBaseVersion_AndResultCanBeSavedAsNewVersion()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, node) = await CreateLawAsync(admin);
        var article = await CreateArticleAsync(admin, law, node, "82", "Nitelikli haller");
        var first = await CreateVersionAsync(admin, article, new DateOnly(2005, 6, 1), "Eski metin.");

        var parsed = await (await admin.PostJsonAsync("/api/admin/article-text/parse",
                new ParseArticleTextRequest("(1) Kasten öldürme suçunun;\na) Tasarlayarak,\nİşlenmesi hâlinde ... cezalandırılır.", first.Id)))
            .ReadAsync<List<ParagraphDto>>();

        var paragraph = Assert.Single(parsed);
        Assert.Equal(first.Paragraphs.Single().Id, paragraph.Id);
        Assert.Equal([1, 0], paragraph.Items.Select(i => i.Level));

        var request = new SaveArticleVersionRequest(new DateOnly(2014, 7, 2), null, null, null, null, false,
            [.. parsed.Select(p => new ParagraphInput(p.Id, p.Number, p.Text,
                [.. p.Items.Select(i => new ParagraphItemInput(i.Id, i.Level, i.Label, i.Text))]))]);
        var second = await (await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions", request))
            .ReadAsync<ArticleVersionDetail>(201);

        var stored = await (await admin.GetAsync($"/api/admin/article-versions/{second.Id}")).ReadAsync<ArticleVersionDetail>();
        Assert.Equal(paragraph.Id, stored.Paragraphs.Single().Id);
        Assert.Equal("a)", stored.Paragraphs.Single().Items[0].Label);
    }

    [Fact]
    public async Task ArticleNumber_MustBeUniqueWithinLaw()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, node) = await CreateLawAsync(admin);
        await CreateArticleAsync(admin, law, node, "81", "Kasten öldürme");

        // "Madde 81" de aynı adrese (madde-81) düşer.
        await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
            new SaveArticleRequest(node.Id, "Madde 81", "Tekrar", Visibility.Public, null)).ExpectAsync(400);
    }

    [Fact]
    public async Task LawSlug_CannotCollideWithSitePages()
    {
        var admin = await factory.CreateAdminClientAsync();
        var group = await CreateGroupAsync(admin);

        await admin.PostJsonAsync("/api/admin/laws",
            new SaveLawRequest(group.Id, "1", "Deneme", null, "panel", null, null, null, Visibility.Public, null))
            .ExpectAsync(400);
    }

    [Fact]
    public async Task Node_CannotBeMovedUnderItsOwnDescendant()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, parent) = await CreateLawAsync(admin);
        var child = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/nodes",
                new SaveLawNodeRequest(parent.Id, LawNodeKind.Bolum, "Birinci Bölüm", "Alt", Visibility.Public, null)))
            .ReadAsync<LawNodeSummary>(201);

        await admin.PutJsonAsync($"/api/admin/nodes/{parent.Id}",
            new SaveLawNodeRequest(child.Id, parent.Kind, parent.Number, parent.Title, Visibility.Public, null))
            .ExpectAsync(400);
    }

    [Fact]
    public async Task Node_WithArticles_CannotBeDeleted()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, node) = await CreateLawAsync(admin);
        var article = await CreateArticleAsync(admin, law, node, "1", "Amaç");

        await admin.DeleteAsync($"/api/admin/nodes/{node.Id}").ExpectAsync(400);
        await admin.DeleteAsync($"/api/admin/articles/{article.Id}").ExpectAsync(204);
        await admin.DeleteAsync($"/api/admin/nodes/{node.Id}").ExpectAsync(204);
    }

    [Fact]
    public async Task AdminEndpoints_RequireSuperAdmin()
    {
        var admin = await factory.CreateAdminClientAsync();
        var group = await CreateGroupAsync(admin);
        var request = new SaveLawRequest(group.Id, "1", "Deneme", null, Slug(), null, null, null, Visibility.Public, null);

        await factory.CreateAnonymousClient().PostJsonAsync("/api/admin/laws", request).ExpectAsync(401);
        var standard = await CreateUserClientAsync(admin, "standard");
        await standard.PostJsonAsync("/api/admin/laws", request).ExpectAsync(403);
    }

    private static string Slug() => "t" + Guid.NewGuid().ToString("N")[..12];

    private static async Task<LawGroupSummary> CreateGroupAsync(HttpClient admin) =>
        await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Test Grubu", Slug(), null)))
            .ReadAsync<LawGroupSummary>(201);

    private static async Task<(LawSummary Law, LawNodeSummary Node)> CreateLawAsync(
        HttpClient admin, Visibility visibility = Visibility.Public)
    {
        var group = await CreateGroupAsync(admin);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", Slug(), null, null, null, visibility, null)))
            .ReadAsync<LawSummary>(201);
        var node = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/nodes",
                new SaveLawNodeRequest(null, LawNodeKind.Kitap, "İkinci Kitap", "Özel Hükümler", Visibility.Public, null)))
            .ReadAsync<LawNodeSummary>(201);
        return (law, node);
    }

    private static async Task<ArticleSummary> CreateArticleAsync(
        HttpClient admin, LawSummary law, LawNodeSummary node, string number, string title) =>
        await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(node.Id, number, title, Visibility.Public, null)))
            .ReadAsync<ArticleSummary>(201);

    private static SaveArticleVersionRequest VersionRequest(DateOnly validFrom, string text) =>
        new(validFrom, null, null, null, null, false, [new ParagraphInput(null, 1, text, null)]);

    private static async Task<ArticleVersionDetail> CreateVersionAsync(
        HttpClient admin, ArticleSummary article, DateOnly validFrom, string text) =>
        await (await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions", VersionRequest(validFrom, text)))
            .ReadAsync<ArticleVersionDetail>(201);

    private async Task<HttpClient> CreateUserClientAsync(HttpClient admin, string role)
    {
        var email = $"{Slug()}@test.local";
        const string password = "TestKullanici123";
        await admin.PostJsonAsync("/api/admin/users", new CreateUserRequest(email, "Test Kullanıcı", role, password))
            .ExpectAsync(201);
        return await factory.CreateSignedInClientAsync(email, password);
    }
}
