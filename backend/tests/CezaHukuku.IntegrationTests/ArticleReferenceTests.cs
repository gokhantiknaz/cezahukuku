using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Users;

namespace CezaHukuku.IntegrationTests;

/// <summary>
/// Spec 0005: metindeki parantez içi madde atıfları ("(madde 21, 22)") okuyucunun görebildiği maddeye bağlanır.
/// Pencere maddeyi mevcut madde uç noktasından açar; bu testler hangi numaranın hangi maddeye bağlandığını ve
/// görünmeyen maddenin hiç bağlanmadığını (BR-1) kanıtlar.
/// </summary>
[Collection(ApiCollection.Name)]
public class ArticleReferenceTests(ApiFactory factory)
{
    [Fact]
    public async Task ArticleText_MarksEachNumber_TargetsSameLaw()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, Visibility.Public);
        await CreateArticleAsync(admin, law, "21", "Kast.");
        await CreateArticleAsync(admin, law, "22", "Taksir.");
        const string text = "Manevi unsur (madde 21, 22) ayrıca aranır.";
        await CreateArticleAsync(admin, law, "30", text);

        var detail = await GetArticleAsync(factory.CreateAnonymousClient(), law, "madde-30");

        Assert.Equal([("21", "21", law.Slug, "madde-21"), ("22", "22", law.Slug, "madde-22")],
            detail.References.Select(r => (text.Substring(r.Start, r.Length), r.Target.Number, r.Target.LawSlug, r.Target.ArticleSlug)));
        Assert.All(detail.References, r => Assert.Equal(detail.Version!.Paragraphs.Single().Id, r.ParagraphId));
    }

    [Fact]
    public async Task ArticleText_ReferenceGoesToOwnLaw()
    {
        var admin = await factory.CreateAdminClientAsync();
        var penal = await CreateLawAsync(admin, Visibility.Public);
        var procedure = await CreateLawAsync(admin, Visibility.Public);
        await CreateArticleAsync(admin, penal, "231", "Ceza kanununda 231.");
        await CreateArticleAsync(admin, procedure, "231", "Hükmün açıklanmasının geri bırakılması.");
        // Bentteki atıf da bulunur.
        await CreateArticleAsync(admin, procedure, "232", "Hüküm.",
            items: [new ParagraphItemInput(null, 1, "a)", "Geri bırakma (m. 231) hâlinde,")]);

        var detail = await GetArticleAsync(factory.CreateAnonymousClient(), procedure, "madde-232");

        var reference = Assert.Single(detail.References);
        Assert.Equal((procedure.Slug, "231"), (reference.Target.LawSlug, reference.Target.Number));
        Assert.Equal(detail.Version!.Paragraphs.Single().Items.Single().Id, reference.ItemId);
    }

    [Fact]
    public async Task UnknownNumber_IsNotMarked()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, Visibility.Public);
        await CreateArticleAsync(admin, law, "21", "Kast.");
        await CreateArticleAsync(admin, law, "30", "Bkz. (madde 21, 999).");

        var detail = await GetArticleAsync(admin, law, "madde-30");

        Assert.Equal(["21"], detail.References.Select(r => r.Target.Number));
    }

    [Fact]
    public async Task PrivateArticle_NotMarkedForAnonymous_MarkedForMember()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, Visibility.Public);
        var secretTitle = "Gizli " + Guid.NewGuid().ToString("N")[..8];
        await CreateArticleAsync(admin, law, "21", "Gizli metin.", visibility: Visibility.Private, title: secretTitle);
        await CreateArticleAsync(admin, law, "30", "Bkz. (madde 21).");

        var anonymous = factory.CreateAnonymousClient();
        var body = await (await anonymous.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-30")).Content.ReadAsStringAsync();
        Assert.Empty((await GetArticleAsync(anonymous, law, "madde-30")).References);
        Assert.DoesNotContain(secretTitle, body);

        var member = await CreateUserClientAsync(admin, Roles.Standard);
        Assert.Equal([secretTitle], (await GetArticleAsync(member, law, "madde-30")).References.Select(r => r.Target.Title));
    }

    [Fact]
    public async Task RepealedArticle_IsMarked()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, Visibility.Public);
        await CreateArticleAsync(admin, law, "21", null, repealed: true);
        await CreateArticleAsync(admin, law, "30", "Bkz. (madde 21).");

        var reference = Assert.Single((await GetArticleAsync(admin, law, "madde-30")).References);

        // Pencere maddeyi açınca mülga olduğu görünür.
        var target = await GetArticleAsync(factory.CreateAnonymousClient(), law, reference.Target.ArticleSlug);
        Assert.True(target.Version!.IsRepealed);
    }

    [Fact]
    public async Task CitationsAndReferences_CoexistInSameText()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, Visibility.Public);
        await CreateArticleAsync(admin, law, "13", "On üç.");
        await CreateArticleAsync(admin, law, "14", "On dört.");
        const string text = "13 üncü maddede yazılı suçlar (madde 14) saklıdır.";
        await CreateArticleAsync(admin, law, "30", text);

        var detail = await GetArticleAsync(admin, law, "madde-30");

        Assert.Equal(["13 üncü maddede"], detail.Citations.Select(c => text.Substring(c.Start, c.Length)));
        Assert.Equal(["14"], detail.References.Select(r => text.Substring(r.Start, r.Length)));
    }

    [Fact]
    public async Task AsOf_ReferencesFromShownVersion_AndTargetShowsVersionInForce()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, Visibility.Public);
        var target = await CreateArticleAsync(admin, law, "21", "Eski kast metni.", validFrom: new DateOnly(2005, 6, 1));
        await AddVersionAsync(admin, target, new DateOnly(2020, 1, 1), "Yeni kast metni.");
        var source = await CreateArticleAsync(admin, law, "30", "Eski metinde (madde 21) atıf.", validFrom: new DateOnly(2005, 6, 1));
        await AddVersionAsync(admin, source, new DateOnly(2020, 1, 1), "Yeni metinde atıf yok.");

        var anonymous = factory.CreateAnonymousClient();
        Assert.Empty((await GetArticleAsync(anonymous, law, "madde-30")).References);
        var old = Assert.Single((await GetArticleAsync(anonymous, law, "madde-30?asOf=2010-01-01")).References);

        // Pencere aynı tarihle açılır: o tarihte yürürlükte olan metin gelir.
        var shown = await GetArticleAsync(anonymous, law, $"{old.Target.ArticleSlug}?asOf=2010-01-01");
        Assert.Equal("Eski kast metni.", shown.Version!.Paragraphs.Single().Text);
        Assert.Equal("Yeni kast metni.", (await GetArticleAsync(anonymous, law, old.Target.ArticleSlug)).Version!.Paragraphs.Single().Text);
    }

    private static async Task<ArticleDetailResponse> GetArticleAsync(HttpClient client, LawSummary law, string slugAndQuery) =>
        await (await client.GetAsync($"/api/library/laws/{law.Slug}/articles/{slugAndQuery}")).ReadAsync<ArticleDetailResponse>();

    private async Task<HttpClient> CreateUserClientAsync(HttpClient admin, string role)
    {
        var email = $"t{Guid.NewGuid():N}"[..13] + "@test.local";
        const string password = "TestKullanici123";
        await admin.PostJsonAsync("/api/admin/users", new CreateUserRequest(email, "Test Kullanıcı", role, password)).ExpectAsync(201);
        return await factory.CreateSignedInClientAsync(email, password);
    }

    private static async Task<LawSummary> CreateLawAsync(HttpClient admin, Visibility visibility, string? slug = null)
    {
        var unique = Guid.NewGuid().ToString("N")[..10];
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Atıf penceresi", "t" + unique, null)))
            .ReadAsync<LawGroupSummary>(201);
        return await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "9" + unique[..4], "Deneme Kanunu " + unique, null, slug ?? "t" + unique,
                    null, null, null, visibility, null)))
            .ReadAsync<LawSummary>(201);
    }

    private static async Task<ArticleSummary> CreateArticleAsync(
        HttpClient admin, LawSummary law, string number, string? text, DateOnly? validFrom = null,
        Visibility visibility = Visibility.Public, string? title = null, bool repealed = false,
        List<ParagraphItemInput>? items = null)
    {
        var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, number, title ?? $"Madde {number}", visibility, null)))
            .ReadAsync<ArticleSummary>(201);
        List<ParagraphInput> paragraphs = text is null ? [] : [new ParagraphInput(null, 1, text, items)];
        await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
            new SaveArticleVersionRequest(validFrom, null, null, null, null, repealed, paragraphs)).ExpectAsync(201);
        return article;
    }

    private static Task AddVersionAsync(HttpClient admin, ArticleSummary article, DateOnly validFrom, string text) =>
        admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
            new SaveArticleVersionRequest(validFrom, null, null, null, null, false, [new ParagraphInput(null, 1, text, null)]))
            .ExpectAsync(201);
}
