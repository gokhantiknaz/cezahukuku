using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Search;
using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Users;

namespace CezaHukuku.IntegrationTests;

/// <summary>
/// Spec 0001: okuma ekranının sekmelerinden notu/kararı düzenleme ve kaldırma. Arayüz mevcut yönetici işlemlerini
/// kullanır; bu testler o işlemlerin okuma ekranı, arama, görünürlük ve yetki açısından beklenen sonucu verdiğini kanıtlar.
/// </summary>
[Collection(ApiCollection.Name)]
public class ArticleTabEditTests(ApiFactory factory)
{
    // AC-2
    [Fact]
    public async Task RemovedNote_IsGoneForAnonymous()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, m81, _) = await CreateLawAsync(admin);
        var word = Word();
        var note = await CreateNoteAsync(admin, m81.Id, $"<p>Gerekçe {word}.</p>", Visibility.Public);
        var anonymous = factory.CreateAnonymousClient();
        Assert.Single((await ArticleAsync(anonymous, law, "81")).Notes);

        await admin.DeleteAsync($"/api/admin/notes/{note.Id}").ExpectAsync(204);

        Assert.Empty((await ArticleAsync(anonymous, law, "81")).Notes);
        Assert.Empty((await SearchAsync(anonymous, word, "gerekce")).Hits);
    }

    // AC-3
    [Fact]
    public async Task UpdatedDecision_ShowsInArticleAndSearch()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, m81, _) = await CreateLawAsync(admin);
        var (before, after) = (Word(), Word());
        var decision = await CreateDecisionAsync(admin, $"Eski özet {before}", Visibility.Public, m81.Id);

        await admin.PutJsonAsync($"/api/admin/decisions/{decision.Id}",
            Decision($"Yeni özet {after}", Visibility.Public, "1. Ceza Dairesi")).ExpectAsync(200);

        var anonymous = factory.CreateAnonymousClient();
        var shown = Assert.Single((await ArticleAsync(anonymous, law, "81")).Decisions);
        Assert.Equal(($"Yeni özet {after}", "1. Ceza Dairesi"), (shown.Summary, shown.Chamber));
        Assert.NotEmpty((await SearchAsync(anonymous, after, "ictihat")).Hits);
        Assert.Empty((await SearchAsync(anonymous, before, "ictihat")).Hits);
    }

    // AC-4
    [Fact]
    public async Task DecisionDetail_ListsAllLinkedArticles()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (_, m81, m82) = await CreateLawAsync(admin);
        var decision = await CreateDecisionAsync(admin, "İki maddeye bağlı.", Visibility.Public, m81.Id, m82.Id);

        var detail = await (await admin.GetAsync($"/api/admin/decisions/{decision.Id}")).ReadAsync<AdminDecisionDetail>();
        Assert.Equal(["81", "82"], detail.Articles.Select(a => a.Number));
    }

    // AC-5
    [Fact]
    public async Task UnlinkedDecision_StaysOnOtherArticleAndInList()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, m81, m82) = await CreateLawAsync(admin);
        var word = Word();
        var decision = await CreateDecisionAsync(admin, $"Bağlantı {word}", Visibility.Public, m81.Id, m82.Id);

        await admin.DeleteAsync($"/api/admin/articles/{m81.Id}/decisions/{decision.Id}").ExpectAsync(204);

        var anonymous = factory.CreateAnonymousClient();
        Assert.Empty((await ArticleAsync(anonymous, law, "81")).Decisions);
        Assert.Equal([decision.Id], (await ArticleAsync(anonymous, law, "82")).Decisions.Select(d => d.Id));
        var list = await (await admin.GetAsync($"/api/admin/decisions?q={word}")).ReadAsync<AdminDecisionList>();
        Assert.Equal(1, Assert.Single(list.Items, i => i.Decision.Id == decision.Id).ArticleCount);
    }

    // AC-7 (hassas alan: görünürlük)
    [Fact]
    public async Task VisibilityMadePrivate_HidesNoteAndDecisionFromAnonymous()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, m81, _) = await CreateLawAsync(admin);
        var (noteWord, decisionWord) = (Word(), Word());
        var note = await CreateNoteAsync(admin, m81.Id, $"<p>Not {noteWord}.</p>", Visibility.Public);
        var decision = await CreateDecisionAsync(admin, $"Karar {decisionWord}", Visibility.Public, m81.Id);

        await admin.PutJsonAsync($"/api/admin/notes/{note.Id}",
            new SaveEditorialNoteRequest(EditorialNoteKind.Gerekce, null, $"<p>Not {noteWord}.</p>", Visibility.Private, null)).ExpectAsync(200);
        await admin.PutJsonAsync($"/api/admin/decisions/{decision.Id}", Decision($"Karar {decisionWord}", Visibility.Private)).ExpectAsync(200);

        var anonymous = factory.CreateAnonymousClient();
        var anonymousView = await ArticleAsync(anonymous, law, "81");
        Assert.Empty(anonymousView.Notes);
        Assert.Empty(anonymousView.Decisions);
        Assert.Empty((await SearchAsync(anonymous, noteWord, "gerekce")).Hits);
        Assert.Empty((await SearchAsync(anonymous, decisionWord, "ictihat")).Hits);

        var member = await CreateUserClientAsync(admin, Roles.Standard);
        var memberView = await ArticleAsync(member, law, "81");
        Assert.Equal([note.Id], memberView.Notes.Select(n => n.Id));
        Assert.Equal([decision.Id], memberView.Decisions.Select(d => d.Id));
        Assert.NotEmpty((await SearchAsync(member, noteWord, "gerekce")).Hits);
        Assert.NotEmpty((await SearchAsync(member, decisionWord, "ictihat")).Hits);
    }

    // AC-8 (hassas alan: yetki)
    [Fact]
    public async Task NonAdmins_CannotEditOrRemove()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, m81, _) = await CreateLawAsync(admin);
        var note = await CreateNoteAsync(admin, m81.Id, "<p>Özgün not.</p>", Visibility.Public);
        var decision = await CreateDecisionAsync(admin, "Özgün özet.", Visibility.Public, m81.Id);

        var callers = new (HttpClient Client, int Status)[]
        {
            (factory.CreateAnonymousClient(), 401),
            (await CreateUserClientAsync(admin, Roles.Standard), 403),
            (await CreateUserClientAsync(admin, Roles.Reader), 403),
        };
        foreach (var (client, status) in callers)
        {
            await client.PutJsonAsync($"/api/admin/notes/{note.Id}",
                new SaveEditorialNoteRequest(EditorialNoteKind.Gerekce, null, "<p>Değişti.</p>", Visibility.Public, null)).ExpectAsync(status);
            await client.DeleteAsync($"/api/admin/notes/{note.Id}").ExpectAsync(status);
            await client.PutJsonAsync($"/api/admin/decisions/{decision.Id}", Decision("Değişti.", Visibility.Public)).ExpectAsync(status);
            await client.GetAsync($"/api/admin/decisions/{decision.Id}").ExpectAsync(status);
            await client.DeleteAsync($"/api/admin/articles/{m81.Id}/decisions/{decision.Id}").ExpectAsync(status);
        }

        var view = await ArticleAsync(admin, law, "81");
        Assert.Equal("<p>Özgün not.</p>", Assert.Single(view.Notes).Html);
        Assert.Equal("Özgün özet.", Assert.Single(view.Decisions).Summary);
    }

    // AC-9 (sunucu tarafı: silinmiş kayıt 404; arayüz bunun için kendi mesajını gösterir)
    [Fact]
    public async Task EditingRemovedNoteOrDecision_Returns404()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (_, m81, _) = await CreateLawAsync(admin);
        var note = await CreateNoteAsync(admin, m81.Id, "<p>Silinecek.</p>", Visibility.Public);
        var decision = await CreateDecisionAsync(admin, "Silinecek.", Visibility.Public, m81.Id);
        await admin.DeleteAsync($"/api/admin/notes/{note.Id}").ExpectAsync(204);
        await admin.DeleteAsync($"/api/admin/decisions/{decision.Id}").ExpectAsync(204);

        await admin.PutJsonAsync($"/api/admin/notes/{note.Id}",
            new SaveEditorialNoteRequest(EditorialNoteKind.Gerekce, null, "<p>x</p>", Visibility.Public, null)).ExpectAsync(404);
        await admin.DeleteAsync($"/api/admin/notes/{note.Id}").ExpectAsync(404);
        await admin.GetAsync($"/api/admin/decisions/{decision.Id}").ExpectAsync(404);
        await admin.PutJsonAsync($"/api/admin/decisions/{decision.Id}", Decision("x", Visibility.Public)).ExpectAsync(404);
    }

    private static string Word() => "zq" + Guid.NewGuid().ToString("N")[..8];

    private static SaveDecisionRequest Decision(string summary, Visibility visibility, string chamber = "Ceza Genel Kurulu", params Guid[] articles) =>
        new(Court.Yargitay, chamber, DecisionKind.Ictihat, "2019/" + Guid.NewGuid().ToString("N")[..4], "2020/1", new DateOnly(2020, 3, 12),
            null, null, null, summary, null, null, visibility, [.. articles]);

    private static async Task<EditorialNoteDto> CreateNoteAsync(HttpClient admin, Guid articleId, string html, Visibility visibility) =>
        await (await admin.PostJsonAsync($"/api/admin/articles/{articleId}/notes",
            new SaveEditorialNoteRequest(EditorialNoteKind.Gerekce, null, html, visibility, null))).ReadAsync<EditorialNoteDto>(201);

    private static async Task<DecisionDto> CreateDecisionAsync(HttpClient admin, string summary, Visibility visibility, params Guid[] articles) =>
        (await (await admin.PostJsonAsync("/api/admin/decisions", Decision(summary, visibility, articles: articles)))
            .ReadAsync<AdminDecisionDetail>(201)).Decision;

    private static async Task<ArticleDetailResponse> ArticleAsync(HttpClient client, LawSummary law, string number) =>
        await (await client.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-{number}")).ReadAsync<ArticleDetailResponse>();

    private static async Task<SearchResponse> SearchAsync(HttpClient client, string q, string kinds) =>
        await (await client.GetAsync($"/api/search?q={q}&kinds={kinds}")).ReadAsync<SearchResponse>();

    private async Task<HttpClient> CreateUserClientAsync(HttpClient admin, string role)
    {
        var email = $"t{Guid.NewGuid():N}"[..13] + "@test.local";
        const string password = "TestKullanici123";
        await admin.PostJsonAsync("/api/admin/users", new CreateUserRequest(email, "Test Kullanıcı", role, password)).ExpectAsync(201);
        return await factory.CreateSignedInClientAsync(email, password);
    }

    private static async Task<(LawSummary Law, ArticleSummary M81, ArticleSummary M82)> CreateLawAsync(HttpClient admin)
    {
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups",
                new SaveLawGroupRequest("Sekme düzenleme", "t" + Guid.NewGuid().ToString("N")[..12], null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", "t" + Guid.NewGuid().ToString("N")[..12],
                    null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
        var m81 = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, "81", "Kasten öldürme", Visibility.Public, null))).ReadAsync<ArticleSummary>(201);
        var m82 = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, "82", "Nitelikli haller", Visibility.Public, null))).ReadAsync<ArticleSummary>(201);
        return (law, m81, m82);
    }
}
