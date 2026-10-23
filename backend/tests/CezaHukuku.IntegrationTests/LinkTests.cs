using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class LinkTests(ApiFactory factory)
{
    [Fact]
    public async Task RelatedLink_ShowsInBothArticles_AndCannotBeDuplicatedInReverse()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, articles) = await CreateLawAsync(admin, "81", "82");

        await admin.PostJsonAsync($"/api/admin/articles/{articles["81"].Id}/links",
            new SaveArticleLinkRequest(ArticleLinkKind.Ilgili, articles["82"].Id, null, "nitelikli hâller")).ExpectAsync(201);
        await admin.PostJsonAsync($"/api/admin/articles/{articles["82"].Id}/links",
            new SaveArticleLinkRequest(ArticleLinkKind.Ilgili, articles["81"].Id, null, null)).ExpectAsync(400);

        var m81 = await GetAsync(factory.CreateAnonymousClient(), law, "madde-81");
        var m82 = await GetAsync(factory.CreateAnonymousClient(), law, "madde-82");
        var outgoing = Assert.Single(m81.Links);
        Assert.Equal(("82", false, "nitelikli hâller"), (outgoing.Article?.Number, outgoing.Incoming, outgoing.Note));
        var incoming = Assert.Single(m82.Links);
        Assert.Equal(("81", true), (incoming.Article?.Number, incoming.Incoming));
    }

    [Fact]
    public async Task OldLawCounterpart_ShowsTargetText_AndIncomingOnOldArticle()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (tck, tckArticles) = await CreateLawAsync(admin, "81");
        var (old, oldArticles) = await CreateLawAsync(admin, "448");
        await admin.PostJsonAsync($"/api/admin/articles/{oldArticles["448"].Id}/versions",
            new SaveArticleVersionRequest(null, new DateOnly(2005, 5, 31), null, null, null, false,
                [new ParagraphInput(null, null, "Her kim bir kimseyi öldürürse...", null)])).ExpectAsync(201);

        await admin.PostJsonAsync($"/api/admin/articles/{tckArticles["81"].Id}/links",
            new SaveArticleLinkRequest(ArticleLinkKind.EskiKanunKarsiligi, oldArticles["448"].Id, null, null)).ExpectAsync(201);

        var m81 = await GetAsync(admin, tck, "madde-81");
        var link = Assert.Single(m81.Links);
        Assert.Equal("Her kim bir kimseyi öldürürse...", link.TargetVersion?.Paragraphs.Single().Text);

        var m448 = await GetAsync(admin, old, "madde-448");
        var back = Assert.Single(m448.Links);
        Assert.Equal((ArticleLinkKind.EskiKanunKarsiligi, true, "81"), (back.Kind, back.Incoming, back.Article?.Number));
        Assert.Null(back.TargetVersion);
    }

    [Fact]
    public async Task ExternalLabel_WhenTargetNotInLibrary_AndValidation()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, articles) = await CreateLawAsync(admin, "81");
        var url = $"/api/admin/articles/{articles["81"].Id}/links";

        await admin.PostJsonAsync(url, new SaveArticleLinkRequest(ArticleLinkKind.EskiKanunKarsiligi, null, "765 s. TCK m. 448", "kısmen"))
            .ExpectAsync(201);
        await admin.PostJsonAsync(url, new SaveArticleLinkRequest(ArticleLinkKind.Ilgili, null, null, null)).ExpectAsync(400);
        await admin.PostJsonAsync(url, new SaveArticleLinkRequest(ArticleLinkKind.Ilgili, articles["81"].Id, null, null)).ExpectAsync(400);

        var link = Assert.Single((await GetAsync(admin, law, "madde-81")).Links);
        Assert.Equal(("765 s. TCK m. 448", (LinkedArticleRef?)null), (link.ExternalLabel, link.Article));
    }

    [Fact]
    public async Task LinkToPrivateLaw_IsHiddenFromAnonymous()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, articles) = await CreateLawAsync(admin, "81");
        var (_, hidden) = await CreateLawAsync(admin, Visibility.Private, "5");
        await admin.PostJsonAsync($"/api/admin/articles/{articles["81"].Id}/links",
            new SaveArticleLinkRequest(ArticleLinkKind.Ilgili, hidden["5"].Id, null, null)).ExpectAsync(201);

        Assert.Empty((await GetAsync(factory.CreateAnonymousClient(), law, "madde-81")).Links);
        Assert.Single((await GetAsync(admin, law, "madde-81")).Links);
    }

    [Fact]
    public async Task ArticleSearch_FindsByReferenceOrTitle()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, articles) = await CreateLawAsync(admin, "81", "82");

        var byReference = await (await admin.GetAsync($"/api/admin/article-search?q={law.Number}/81"))
            .ReadAsync<List<ArticleSearchHit>>();
        Assert.Equal([articles["81"].Id], byReference.Select(h => h.ArticleId));

        var byTitle = await (await admin.GetAsync($"/api/admin/article-search?q={Uri.EscapeDataString(articles["82"].Title)}"))
            .ReadAsync<List<ArticleSearchHit>>();
        Assert.Contains(byTitle, h => h.ArticleId == articles["82"].Id);
    }

    private static async Task<ArticleDetailResponse> GetAsync(HttpClient client, LawSummary law, string slug) =>
        await (await client.GetAsync($"/api/library/laws/{law.Slug}/articles/{slug}")).ReadAsync<ArticleDetailResponse>();

    private static Task<(LawSummary Law, Dictionary<string, ArticleSummary> Articles)> CreateLawAsync(
        HttpClient admin, params string[] numbers) => CreateLawAsync(admin, Visibility.Public, numbers);

    private static async Task<(LawSummary Law, Dictionary<string, ArticleSummary> Articles)> CreateLawAsync(
        HttpClient admin, Visibility visibility, params string[] numbers)
    {
        var unique = Guid.NewGuid().ToString("N")[..10];
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Bağlantılar", "t" + unique, null)))
            .ReadAsync<LawGroupSummary>(201);
        // Kanun numarası testler arasında tekil: madde göndermesiyle arama ("9xxxx/81") yalnız bu kanunu bulsun.
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "9" + Random.Shared.Next(100000, 999999), "Deneme Kanunu", null, "t" + unique,
                    null, null, null, visibility, null)))
            .ReadAsync<LawSummary>(201);
        var articles = new Dictionary<string, ArticleSummary>();
        foreach (var number in numbers)
        {
            articles[number] = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                    new SaveArticleRequest(null, number, $"Başlık {number} {unique}", Visibility.Public, null)))
                .ReadAsync<ArticleSummary>(201);
        }
        return (law, articles);
    }
}
