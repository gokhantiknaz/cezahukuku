using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Personal;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Domain.Users;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HighlightTests(ApiFactory factory)
{
    private const string Text = "Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.";

    [Fact]
    public async Task Highlight_IsCreatedListedRecoloredAndDeleted()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (article, version) = await CreateArticleAsync(admin, Text);
        var user = await CreateUserClientAsync(admin, Roles.Standard);

        var created = await (await user.PostJsonAsync("/api/me/highlights",
                new CreateHighlightRequest(version.Id, HighlightColor.Yellow, [Range(version, "müebbet hapis")])))
            .ReadAsync<HighlightDto>(201);
        Assert.Equal(("müebbet hapis", false), (created.Quote, created.IsStale));

        await user.PutJsonAsync($"/api/me/highlights/{created.Id}", new UpdateHighlightRequest(HighlightColor.Green)).ExpectAsync(204);
        var listed = Assert.Single(await ListAsync(user, article));
        Assert.Equal(HighlightColor.Green, listed.Color);
        Assert.Equal(Range(version, "müebbet hapis"), Assert.Single(listed.Segments));

        await user.DeleteAsync($"/api/me/highlights/{created.Id}").ExpectAsync(204);
        Assert.Empty(await ListAsync(user, article));
    }

    [Fact]
    public async Task Highlight_SpansParagraphsAndClauses()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (article, _) = await CreateArticleAsync(admin, Text);
        var version = await (await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
                new SaveArticleVersionRequest(new DateOnly(2010, 1, 1), null, null, null, null, false,
                [
                    new ParagraphInput(null, 1, "Kasten öldürme suçunun;", [new ParagraphItemInput(null, 1, "a)", "Tasarlayarak,")]),
                    new ParagraphInput(null, 2, "İşlenmesi hâlinde ağırlaştırılmış müebbet hapis cezası verilir.", null),
                ])))
            .ReadAsync<ArticleVersionDetail>(201);
        var user = await CreateUserClientAsync(admin, Roles.Standard);
        var paragraph = version.Paragraphs[0];

        var created = await (await user.PostJsonAsync("/api/me/highlights",
                new CreateHighlightRequest(version.Id, HighlightColor.Blue,
                [
                    new TextPosition(paragraph.Id, null, paragraph.Text.IndexOf("öldürme", StringComparison.Ordinal), "öldürme suçunun".Length),
                    new TextPosition(paragraph.Id, paragraph.Items[0].Id, 0, "Tasarlayarak".Length),
                    new TextPosition(version.Paragraphs[1].Id, null, 0, "İşlenmesi".Length),
                ])))
            .ReadAsync<HighlightDto>(201);

        Assert.Equal("öldürme suçunun … Tasarlayarak … İşlenmesi", created.Quote);
        Assert.Equal(3, created.Segments.Count);
    }

    [Fact]
    public async Task Highlights_ArePrivateToOwner()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (article, version) = await CreateArticleAsync(admin, Text);
        var owner = await CreateUserClientAsync(admin, Roles.Standard);
        var other = await CreateUserClientAsync(admin, Roles.Standard);
        var created = await (await owner.PostJsonAsync("/api/me/highlights",
                new CreateHighlightRequest(version.Id, HighlightColor.Yellow, [Range(version, "kasten")])))
            .ReadAsync<HighlightDto>(201);

        Assert.Empty(await ListAsync(other, article));
        await other.PutJsonAsync($"/api/me/highlights/{created.Id}", new UpdateHighlightRequest(HighlightColor.Blue)).ExpectAsync(404);
        await other.DeleteAsync($"/api/me/highlights/{created.Id}").ExpectAsync(404);
        Assert.Single(await ListAsync(owner, article));
    }

    [Fact]
    public async Task ReaderAndAnonymous_CannotUsePersonalLayer()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (article, version) = await CreateArticleAsync(admin, Text);
        var reader = await CreateUserClientAsync(admin, Roles.Reader);
        var request = new CreateHighlightRequest(version.Id, HighlightColor.Yellow, [Range(version, "kasten")]);

        await reader.PostJsonAsync("/api/me/highlights", request).ExpectAsync(403);
        await reader.GetAsync($"/api/me/articles/{article.Id}/highlights").ExpectAsync(403);
        await factory.CreateAnonymousClient().PostJsonAsync("/api/me/highlights", request).ExpectAsync(401);
    }

    [Fact]
    public async Task Selection_MustMatchVersionText()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (_, version) = await CreateArticleAsync(admin, Text);
        var user = await CreateUserClientAsync(admin, Roles.Standard);
        var paragraphId = version.Paragraphs[0].Id;

        await user.PostJsonAsync("/api/me/highlights",
            new CreateHighlightRequest(version.Id, HighlightColor.Yellow, [new TextPosition(paragraphId, null, 60, 50)])).ExpectAsync(400);
        await user.PostJsonAsync("/api/me/highlights",
            new CreateHighlightRequest(version.Id, HighlightColor.Yellow, [new TextPosition(Guid.NewGuid(), null, 0, 3)])).ExpectAsync(400);
        await user.PostJsonAsync("/api/me/highlights",
            new CreateHighlightRequest(version.Id, HighlightColor.Yellow, [])).ExpectAsync(400);
        await user.PostJsonAsync("/api/me/highlights",
            new CreateHighlightRequest(Guid.NewGuid(), HighlightColor.Yellow, [new TextPosition(paragraphId, null, 0, 3)])).ExpectAsync(404);
    }

    [Fact]
    public async Task NewVersion_ReanchorsHighlight_OrMarksItStale()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (article, first) = await CreateArticleAsync(admin, Text);
        var user = await CreateUserClientAsync(admin, Roles.Standard);
        await user.PostJsonAsync("/api/me/highlights",
            new CreateHighlightRequest(first.Id, HighlightColor.Yellow, [Range(first, "hapis cezası")])).ExpectAsync(201);
        await user.PostJsonAsync("/api/me/highlights",
            new CreateHighlightRequest(first.Id, HighlightColor.Orange, [Range(first, "müebbet")])).ExpectAsync(201);

        // Yeni sürümde "müebbet" kalktı, "hapis cezası" başka bir konuma kaydı.
        const string amended = "Bir insanı kasten ve tasarlayarak öldüren kişi, ağırlaştırılmış hapis cezası ile cezalandırılır.";
        var second = await (await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
                new SaveArticleVersionRequest(new DateOnly(2010, 1, 1), null, null, null, null, false,
                    [new ParagraphInput(first.Paragraphs[0].Id, 1, amended, null)])))
            .ReadAsync<ArticleVersionDetail>(201);

        var current = await ListAsync(user, article);
        var moved = current.Single(h => h.Color == HighlightColor.Yellow);
        Assert.False(moved.IsStale);
        Assert.Equal(Range(second, "hapis cezası"), Assert.Single(moved.Segments));
        var gone = current.Single(h => h.Color == HighlightColor.Orange);
        Assert.True(gone.IsStale);
        Assert.Empty(gone.Segments);
        Assert.Equal("müebbet", gone.Quote);

        // Eski sürüm (suç tarihindeki metin) açıldığında ikisi de ilk konumunda.
        var old = await ListAsync(user, article, first.Id);
        Assert.All(old, h => Assert.False(h.IsStale));
    }

    private static async Task<List<HighlightDto>> ListAsync(HttpClient client, ArticleSummary article, Guid? versionId = null) =>
        await (await client.GetAsync($"/api/me/articles/{article.Id}/highlights{(versionId is { } id ? $"?versionId={id}" : "")}"))
            .ReadAsync<List<HighlightDto>>();

    private static TextPosition Range(ArticleVersionDetail version, string quote)
    {
        var paragraph = version.Paragraphs[0];
        return new TextPosition(paragraph.Id, null, paragraph.Text.IndexOf(quote, StringComparison.Ordinal), quote.Length);
    }

    private static async Task<(ArticleSummary Article, ArticleVersionDetail Version)> CreateArticleAsync(HttpClient admin, string text)
    {
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups",
                new SaveLawGroupRequest("Vurgular", Slug(), null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", Slug(), null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
        var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, "81", "Kasten öldürme", Visibility.Public, null)))
            .ReadAsync<ArticleSummary>(201);
        var version = await (await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
                new SaveArticleVersionRequest(new DateOnly(2005, 6, 1), null, null, null, null, false,
                    [new ParagraphInput(null, 1, text, null)])))
            .ReadAsync<ArticleVersionDetail>(201);
        return (article, version);
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
