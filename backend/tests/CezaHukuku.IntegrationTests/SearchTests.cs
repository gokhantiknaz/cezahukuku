using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Personal;
using CezaHukuku.Api.Search;
using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Domain.Users;
using CezaHukuku.Infrastructure.Search;

namespace CezaHukuku.IntegrationTests;

/// <summary>
/// Arama: Türkçe kök/aksansız eşleşme, sorgu dili, kısayol, filtreler, görünürlük ve kişisel kayıtlar.
/// Testler aynı veritabanını paylaştığından her biri kendi kanununu açar ve aramayı o kanunla sınırlar.
/// </summary>
[Collection(ApiCollection.Name)]
public class SearchTests(ApiFactory factory)
{
    private const string Text81 = "Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.";
    private const string Text83 = "Kasten öldürmenin ihmali davranışla işlenmesi hâlinde verilecek ceza, müebbet hapis cezasıdır.";
    private const string Text85 = "Taksirle bir insanın ölümüne neden olan kişi, iki yıldan altı yıla kadar hapis cezası ile cezalandırılır; ihmal hâlinde de.";

    [Theory]
    // 85'in başlığı "Taksirle öldürme".
    [InlineData("oldurme", new[] { "81", "83", "85" })] // aksansız yazım + gövde öneki
    [InlineData("öldürmenin", new[] { "81", "83", "85" })] // çekimli biçim
    [InlineData("öldüren", new[] { "81", "83", "85" })] // fiilden türeyen biçim
    [InlineData("kasten öldüren", new[] { "81", "83" })]
    [InlineData("İHMAL", new[] { "83", "85" })] // büyük harf + Türkçe İ
    [InlineData("\"iki yıldan altı yıla\"", new[] { "85" })] // tam ifade
    [InlineData("\"altı yıldan iki yıla\"", new string[0])] // sözcükler başka sırada
    [InlineData("(\"müebbet\" VE \"ihmal\") -taksir", new[] { "83" })]
    [InlineData("taksir VEYA tasarlayarak", new[] { "85" })]
    public async Task TurkishQueries_FindExpectedArticles(string query, string[] expected)
    {
        var (admin, law, _) = await CreateLawAsync();

        var response = await SearchAsync(admin, query, law, "article");

        Assert.Equal(expected.Order(), response.Hits.Select(h => h.Title.Split(' ')[1]).Order());
    }

    [Fact]
    public async Task Snippet_MarksMatchedWords()
    {
        var (admin, law, _) = await CreateLawAsync();

        var hit = Assert.Single((await SearchAsync(admin, "taksirle", law, "article")).Hits);

        Assert.Contains("\u0002Taksirle\u0003", hit.Snippet);
        Assert.Equal(("TCK", $"/{law.Slug}/madde-85"), (hit.Context, hit.Url));
    }

    [Fact]
    public async Task ArticleReference_ReturnsJump()
    {
        var (admin, law, _) = await CreateLawAsync();

        // Test kanunlarının hepsi "TCK"; kısayol kanunun adres adıyla da çalışır.
        var response = await (await admin.GetAsync($"/api/search?q={law.Slug} 83")).ReadAsync<SearchResponse>();

        Assert.Equal(new SearchJump("TCK Madde 83 — Kasten öldürmenin ihmali davranışla işlenmesi", $"/{law.Slug}/madde-83"), response.Jump);
    }

    [Fact]
    public async Task NotesAndDecisions_AreSearchable_AndKindsFilter()
    {
        var (admin, law, articles) = await CreateLawAsync();
        await admin.PostJsonAsync($"/api/admin/articles/{articles["81"].Id}/notes",
            new SaveEditorialNoteRequest(EditorialNoteKind.Gerekce, null, "<p>Yaşam hakkı en temel haktır; kasten öldürme ağır yaptırıma bağlanmıştır.</p>",
                Visibility.Public, null)).ExpectAsync(201);
        await admin.PostJsonAsync("/api/admin/decisions",
            new SaveDecisionRequest(Court.Yargitay, "1. Ceza Dairesi", DecisionKind.Ictihat, "2019/1234", "2020/567",
                new DateOnly(2020, 3, 12), null, null, null, "Olası kastla yaşam hakkının ihlali.", "", null, Visibility.Public,
                [articles["81"].Id])).ExpectAsync(201);

        var all = await SearchAsync(admin, "yaşam hakkı", law);
        Assert.Equal([SearchKind.Gerekce, SearchKind.Ictihat], all.Hits.Select(h => h.Kind).Order());
        var ictihat = all.Hits.Single(h => h.Kind == SearchKind.Ictihat);
        Assert.Equal(("Yargıtay 1. Ceza Dairesi, 12.03.2020, E. 2019/1234, K. 2020/567", $"/{law.Slug}/madde-81?sekme=ictihatlar"),
            (ictihat.Title, ictihat.Url));

        var onlyNotes = await SearchAsync(admin, "yaşam hakkı", law, "gerekce");
        Assert.Equal([SearchKind.Gerekce], onlyNotes.Hits.Select(h => h.Kind));
        Assert.Equal(2, onlyNotes.Counts.Sum(c => c.Count)); // sayılar tür filtresinden bağımsız
    }

    [Fact]
    public async Task PrivateContent_IsHiddenFromAnonymous_AndFollowsVisibilityChanges()
    {
        var (admin, law, articles) = await CreateLawAsync();
        var anonymous = factory.CreateAnonymousClient();
        Assert.Single((await SearchAsync(anonymous, "taksirle", law)).Hits);

        await admin.PutJsonAsync($"/api/admin/articles/{articles["85"].Id}",
            new SaveArticleRequest(null, "85", "Taksirle öldürme", Visibility.Private, null)).ExpectAsync(200);

        Assert.Empty((await SearchAsync(anonymous, "taksirle", law)).Hits);
        Assert.Single((await SearchAsync(admin, "taksirle", law)).Hits);
        Assert.Null((await (await anonymous.GetAsync($"/api/search?q={law.Slug} 85")).ReadAsync<SearchResponse>()).Jump);
    }

    [Fact]
    public async Task DeletedArticle_AndNewVersionText_UpdateIndex()
    {
        var (admin, law, articles) = await CreateLawAsync();

        await admin.PostJsonAsync($"/api/admin/articles/{articles["81"].Id}/versions",
            new SaveArticleVersionRequest(new DateOnly(2010, 1, 1), null, null, null, null, false,
                [new ParagraphInput(null, 1, "Bir insanı canavarca hisle öldüren kişi cezalandırılır.", null)])).ExpectAsync(201);
        Assert.Single((await SearchAsync(admin, "canavarca", law)).Hits);
        Assert.DoesNotContain((await SearchAsync(admin, "müebbet", law)).Hits, h => h.Title.StartsWith("Madde 81"));

        await admin.DeleteAsync($"/api/admin/articles/{articles["83"].Id}").ExpectAsync(204);
        Assert.Empty((await SearchAsync(admin, "ihmali davranışla", law)).Hits);
    }

    [Fact]
    public async Task PersonalNotes_AreFoundOnlyByOwner()
    {
        var (admin, law, articles) = await CreateLawAsync();
        var owner = await CreateUserClientAsync(admin, Roles.Standard);
        var other = await CreateUserClientAsync(admin, Roles.Standard);
        await owner.PostJsonAsync("/api/me/notes", new CreatePersonalNoteRequest(articles["81"].Id, null, "Zincirleme suç ile karıştırma.")).ExpectAsync(201);

        var mine = await SearchAsync(owner, "zincirleme", law, mine: true);
        var hit = Assert.Single(mine.Hits);
        Assert.Equal((SearchKind.Note, $"/{law.Slug}/madde-81"), (hit.Kind, hit.Url));

        Assert.Empty((await SearchAsync(other, "zincirleme", law)).Hits);
        Assert.Empty((await SearchAsync(admin, "zincirleme", law)).Hits);
        Assert.Empty((await SearchAsync(factory.CreateAnonymousClient(), "zincirleme", law)).Hits);
    }

    [Fact]
    public async Task Highlight_IsSearchableByOwner_AndRemovedWhenDeleted()
    {
        var (admin, law, articles) = await CreateLawAsync();
        var user = await CreateUserClientAsync(admin, Roles.Standard);
        var detail = await (await user.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-85")).ReadAsync<ArticleDetailResponse>();
        var paragraph = detail.Version!.Paragraphs[0];
        var highlight = await (await user.PostJsonAsync("/api/me/highlights", new CreateHighlightRequest(detail.Version.Id, HighlightColor.Yellow,
                [new TextPosition(paragraph.Id, null, paragraph.Text.IndexOf("iki yıldan", StringComparison.Ordinal), "iki yıldan altı yıla".Length)])))
            .ReadAsync<HighlightDto>(201);

        Assert.Contains((await SearchAsync(user, "altı yıla", law, mine: true)).Hits, h => h.Kind == SearchKind.Highlight);

        await user.DeleteAsync($"/api/me/highlights/{highlight.Id}").ExpectAsync(204);
        Assert.Empty((await SearchAsync(user, "altı yıla", law, mine: true)).Hits);
    }

    [Fact]
    public async Task EmptyOrNegativeOnlyQuery_ReturnsNothing()
    {
        var (admin, law, _) = await CreateLawAsync();
        Assert.Empty((await SearchAsync(admin, "-taksir", law)).Hits);
        Assert.Empty((await SearchAsync(admin, "   ", law)).Hits);
    }

    private static async Task<SearchResponse> SearchAsync(HttpClient client, string q, LawSummary law, string? kinds = null, bool mine = false)
    {
        var url = $"/api/search?q={Uri.EscapeDataString(q)}&laws={law.Slug}{(kinds is null ? "" : $"&kinds={kinds}")}{(mine ? "&mine=true" : "")}";
        return await (await client.GetAsync(url)).ReadAsync<SearchResponse>();
    }

    private async Task<(HttpClient Admin, LawSummary Law, Dictionary<string, ArticleSummary> Articles)> CreateLawAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Arama", Slug(), null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", Slug(), null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);

        var articles = new Dictionary<string, ArticleSummary>();
        foreach (var (number, title, text) in new[]
                 {
                     ("81", "Kasten öldürme", Text81),
                     ("83", "Kasten öldürmenin ihmali davranışla işlenmesi", Text83),
                     ("85", "Taksirle öldürme", Text85),
                 })
        {
            var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                    new SaveArticleRequest(null, number, title, Visibility.Public, null)))
                .ReadAsync<ArticleSummary>(201);
            await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
                new SaveArticleVersionRequest(new DateOnly(2005, 6, 1), null, null, null, null, false,
                    [new ParagraphInput(null, 1, text, null)])).ExpectAsync(201);
            articles[number] = article;
        }
        return (admin, law, articles);
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
