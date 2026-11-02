using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Personal;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Domain.Users;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class NotebookTests(ApiFactory factory)
{
    private const string Text = "Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.";

    [Fact]
    public async Task Notebook_ListsHighlightsAndNotes_ByArticle_InTextOrder()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin);
        var (first, firstVersion) = await CreateArticleAsync(admin, law, "81");
        var (second, secondVersion) = await CreateArticleAsync(admin, law, "82");
        var user = await CreateUserClientAsync(admin, Roles.Standard);

        // İkinci maddeye önce, birinci maddede metnin sonundan başa doğru eklenir: liste yine kütüphane ve metin sırasında.
        await HighlightAsync(user, secondVersion, "kasten", HighlightColor.Green);
        var late = await HighlightAsync(user, firstVersion, "müebbet hapis", HighlightColor.Blue);
        var early = await HighlightAsync(user, firstVersion, "kasten", HighlightColor.Yellow);
        await user.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(first.Id, late.Id, "Vurgunun notu.")).ExpectAsync(201);
        await user.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(first.Id, null, "Madde notu.")).ExpectAsync(201);

        var notebook = await GetAsync(user);

        Assert.Equal([first.Id, second.Id], notebook.Select(a => a.Article.ArticleId));
        var entry = notebook[0];
        Assert.Equal(law.Slug, entry.Article.LawSlug);
        Assert.Equal([early.Id, late.Id], entry.Highlights.Select(h => h.Id));
        Assert.Equal(("müebbet hapis", HighlightColor.Blue, "Vurgunun notu."),
            (entry.Highlights[1].Quote, entry.Highlights[1].Color, entry.Highlights[1].Note?.Text));
        Assert.Null(entry.Highlights[0].Note);
        Assert.Equal(["Madde notu."], entry.Notes.Select(n => n.Text));
    }

    [Fact]
    public async Task Notebook_IsPrivate_MarksStale_AndSkipsDeletedArticles()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin);
        var (article, version) = await CreateArticleAsync(admin, law, "81");
        var (removed, removedVersion) = await CreateArticleAsync(admin, law, "82");
        var owner = await CreateUserClientAsync(admin, Roles.Standard);
        await HighlightAsync(owner, version, "müebbet hapis", HighlightColor.Yellow);
        await HighlightAsync(owner, removedVersion, "kasten", HighlightColor.Yellow);

        Assert.Empty(await GetAsync(await CreateUserClientAsync(admin, Roles.Standard)));
        await (await CreateUserClientAsync(admin, Roles.Reader)).GetAsync("/api/me/notebook").ExpectAsync(403);
        await factory.CreateAnonymousClient().GetAsync("/api/me/notebook").ExpectAsync(401);

        // Madde değişti: vurgulanan ifade yürürlükteki metinde yok.
        await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
                new SaveArticleVersionRequest(new DateOnly(2020, 1, 1), null, null, null, null, false,
                    [new ParagraphInput(null, 1, "Bir insanı kasten öldüren kişi, ağır hapis cezası ile cezalandırılır.", null)]))
            .ExpectAsync(201);
        await admin.DeleteAsync($"/api/admin/articles/{removed.Id}").ExpectAsync(204);

        var entry = Assert.Single(await GetAsync(owner));
        Assert.Equal(article.Id, entry.Article.ArticleId);
        Assert.True(Assert.Single(entry.Highlights).IsStale);
    }

    private static async Task<List<NotebookArticleDto>> GetAsync(HttpClient client) =>
        await (await client.GetAsync("/api/me/notebook")).ReadAsync<List<NotebookArticleDto>>();

    private static async Task<HighlightDto> HighlightAsync(
        HttpClient user, ArticleVersionDetail version, string quote, HighlightColor color)
    {
        var paragraph = version.Paragraphs[0];
        var range = new TextPosition(paragraph.Id, null, paragraph.Text.IndexOf(quote, StringComparison.Ordinal), quote.Length);
        return await (await user.PostJsonAsync("/api/me/highlights", new CreateHighlightRequest(version.Id, color, [range])))
            .ReadAsync<HighlightDto>(201);
    }

    private static async Task<LawSummary> CreateLawAsync(HttpClient admin)
    {
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Defter", Slug(), null)))
            .ReadAsync<LawGroupSummary>(201);
        return await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", Slug(), null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
    }

    private static async Task<(ArticleSummary Article, ArticleVersionDetail Version)> CreateArticleAsync(
        HttpClient admin, LawSummary law, string number)
    {
        var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, number, "Kasten öldürme", Visibility.Public, null)))
            .ReadAsync<ArticleSummary>(201);
        var version = await (await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
                new SaveArticleVersionRequest(new DateOnly(2005, 6, 1), null, null, null, null, false,
                    [new ParagraphInput(null, 1, Text, null)])))
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
