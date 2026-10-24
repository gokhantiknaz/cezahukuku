using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Personal;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Domain.Users;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PersonalNoteTests(ApiFactory factory)
{
    private const string Text = "Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.";

    [Fact]
    public async Task NoteOnHighlight_IsCreatedUpdatedAndListed()
    {
        var (article, version, user) = await SetUpAsync();
        var highlight = await HighlightAsync(user, version, "müebbet hapis");

        var note = await (await user.PostJsonAsync("/api/me/notes",
                new CreatePersonalNoteRequest(article.Id, highlight.Id, "  Ağırlaştırılmış müebbet değil.\r\nYHGK 2019/1  ")))
            .ReadAsync<PersonalNoteDto>(201);
        Assert.Equal("Ağırlaştırılmış müebbet değil.\nYHGK 2019/1", note.Text);

        var updated = await (await user.PutJsonAsync($"/api/me/notes/{note.Id}", new UpdatePersonalNoteRequest("Güncel not.")))
            .ReadAsync<PersonalNoteDto>();
        Assert.Equal("Güncel not.", updated.Text);

        var listed = Assert.Single(await ListAsync(user, article));
        Assert.Equal((highlight.Id, "Güncel not."), (listed.HighlightId, listed.Text));
    }

    [Fact]
    public async Task ArticleNote_WithoutHighlight_IsAllowedMoreThanOnce()
    {
        var (article, _, user) = await SetUpAsync();

        await user.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, null, "Birinci.")).ExpectAsync(201);
        await user.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, null, "İkinci.")).ExpectAsync(201);

        Assert.Equal(["Birinci.", "İkinci."], (await ListAsync(user, article)).Select(n => n.Text));
    }

    [Fact]
    public async Task Highlight_HasAtMostOneNote_AndTextIsRequired()
    {
        var (article, version, user) = await SetUpAsync();
        var highlight = await HighlightAsync(user, version, "kasten");

        await user.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, highlight.Id, "   ")).ExpectAsync(400);
        await user.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, highlight.Id, "Bir.")).ExpectAsync(201);
        await user.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, highlight.Id, "İki.")).ExpectAsync(400);
        await user.PostJsonAsync("/api/me/notes",
            new CreatePersonalNoteRequest(article.Id, null, new string('a', PersonalNote.MaxTextLength + 1))).ExpectAsync(400);
    }

    [Fact]
    public async Task DeletingHighlight_DeletesItsNote()
    {
        var (article, version, user) = await SetUpAsync();
        var highlight = await HighlightAsync(user, version, "kasten");
        await user.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, highlight.Id, "Vurgunun notu.")).ExpectAsync(201);
        await user.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, null, "Madde notu.")).ExpectAsync(201);

        await user.DeleteAsync($"/api/me/highlights/{highlight.Id}").ExpectAsync(204);

        Assert.Equal(["Madde notu."], (await ListAsync(user, article)).Select(n => n.Text));
        // Silinen vurgunun yerine yeni vurgu yapılıp not eklenebilir (tekillik yalnız silinmemiş notlarda).
        var again = await HighlightAsync(user, version, "kasten");
        await user.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, again.Id, "Yeni.")).ExpectAsync(201);
    }

    [Fact]
    public async Task Notes_ArePrivateToOwner()
    {
        var (article, version, owner) = await SetUpAsync();
        var admin = await factory.CreateAdminClientAsync();
        var other = await CreateUserClientAsync(admin, Roles.Standard);
        var highlight = await HighlightAsync(owner, version, "kasten");
        var note = await (await owner.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, highlight.Id, "Gizli.")))
            .ReadAsync<PersonalNoteDto>(201);

        Assert.Empty(await ListAsync(other, article));
        await other.PutJsonAsync($"/api/me/notes/{note.Id}", new UpdatePersonalNoteRequest("Değiştir.")).ExpectAsync(404);
        await other.DeleteAsync($"/api/me/notes/{note.Id}").ExpectAsync(404);
        // Başkasının vurgusuna not eklenemez.
        await other.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, highlight.Id, "Benim.")).ExpectAsync(404);

        var reader = await CreateUserClientAsync(admin, Roles.Reader);
        await reader.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(article.Id, null, "Okuyucu.")).ExpectAsync(403);

        await owner.DeleteAsync($"/api/me/notes/{note.Id}").ExpectAsync(204);
        Assert.Empty(await ListAsync(owner, article));
    }

    private static async Task<List<PersonalNoteDto>> ListAsync(HttpClient client, ArticleSummary article) =>
        await (await client.GetAsync($"/api/me/articles/{article.Id}/notes")).ReadAsync<List<PersonalNoteDto>>();

    private static async Task<HighlightDto> HighlightAsync(HttpClient user, ArticleVersionDetail version, string quote)
    {
        var paragraph = version.Paragraphs[0];
        var range = new TextPosition(paragraph.Id, null, paragraph.Text.IndexOf(quote, StringComparison.Ordinal), quote.Length);
        return await (await user.PostJsonAsync("/api/me/highlights", new CreateHighlightRequest(version.Id, HighlightColor.Yellow, [range])))
            .ReadAsync<HighlightDto>(201);
    }

    private async Task<(ArticleSummary Article, ArticleVersionDetail Version, HttpClient User)> SetUpAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Notlarım", Slug(), null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", Slug(), null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
        var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, "81", "Kasten öldürme", Visibility.Public, null)))
            .ReadAsync<ArticleSummary>(201);
        var version = await (await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
                new SaveArticleVersionRequest(new DateOnly(2005, 6, 1), null, null, null, null, false,
                    [new ParagraphInput(null, 1, Text, null)])))
            .ReadAsync<ArticleVersionDetail>(201);
        return (article, version, await CreateUserClientAsync(admin, Roles.Standard));
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
