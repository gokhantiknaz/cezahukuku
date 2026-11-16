using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Content;
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
public partial class ArticleReferenceTests(ApiFactory factory)
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

    [Fact]
    public async Task LectureNote_LawFromNoteLink_ElseTck()
    {
        var admin = await factory.CreateAdminClientAsync();
        var tck = await EnsureTckAsync(admin);
        var procedure = await CreateLawAsync(admin, Visibility.Public);
        await CreateArticleAsync(admin, procedure, "231", "Hükmün açıklanmasının geri bırakılması.");
        var tag = Guid.NewGuid().ToString("N")[..8];

        var linked = await CreateNoteAsync(admin, $"Bağlı {tag}", "<p>Geri bırakma (madde 231).</p>", lawId: procedure.Id);
        var plain = await CreateNoteAsync(admin, $"Bağsız {tag}", "<p>Kast (madde 21).</p>");
        var groupOnly = await CreateNoteAsync(admin, $"Grup {tag}", "<p>Kast (madde 21).</p>", groupId: procedure.GroupId);

        var course = await (await admin.PostJsonAsync("/api/admin/lecture-courses", new SaveLectureCourseRequest($"Ders {tag}", null, Visibility.Public)))
            .ReadAsync<LectureCourseDetail>(201);
        var linkedHeading = await AddHeadingAsync(admin, course.Id, $"Başlık bağlı {tag}", "<p>(md. 231)</p>", procedure.Id);
        var plainHeading = await AddHeadingAsync(admin, course.Id, $"Başlık bağsız {tag}", "<p>(m. 21)</p>", null);

        var anonymous = factory.CreateAnonymousClient();
        Assert.Equal([$"{procedure.Slug}/madde-231"], ReferencesIn(await GetNoteAsync(anonymous, linked.Slug)));
        Assert.Equal([$"{tck.Slug}/madde-21"], ReferencesIn(await GetNoteAsync(anonymous, plain.Slug)));
        Assert.Equal([$"{tck.Slug}/madde-21"], ReferencesIn(await GetNoteAsync(anonymous, groupOnly.Slug)));
        Assert.Equal([$"{procedure.Slug}/madde-231"], ReferencesIn(await GetNoteAsync(anonymous, linkedHeading.Slug)));
        Assert.Equal([$"{tck.Slug}/madde-21"], ReferencesIn(await GetNoteAsync(anonymous, plainHeading.Slug)));
    }

    [Fact]
    public async Task LectureNote_PrivateTarget_NotMarkedForAnonymous_MarkedForMember()
    {
        var admin = await factory.CreateAdminClientAsync();
        var secretLaw = await CreateLawAsync(admin, Visibility.Private);
        await CreateArticleAsync(admin, secretLaw, "5", "Gizli kanunun metni.");
        var note = await CreateNoteAsync(admin, $"Açık not {Guid.NewGuid():N}"[..20], "<p>Bkz. (madde 5).</p>", lawId: secretLaw.Id);

        var anonymous = factory.CreateAnonymousClient();
        var body = await (await anonymous.GetAsync($"/api/lecture-notes/{note.Slug}")).Content.ReadAsStringAsync();
        Assert.Empty(ReferencesIn(await GetNoteAsync(anonymous, note.Slug)));
        // Gizli kanunun adı ve maddesinin adresi yanıtta yok (grubu herkese açıktır, o görünebilir).
        Assert.DoesNotContain(secretLaw.Name, body);
        Assert.DoesNotContain($"{secretLaw.Slug}/madde-5", body);

        var member = await CreateUserClientAsync(admin, Roles.Standard);
        Assert.Equal([$"{secretLaw.Slug}/madde-5"], ReferencesIn(await GetNoteAsync(member, note.Slug)));

        // Açık kanunun gizli maddesi de (I-2): anonim okuyucuya yalnız açık madde işaretli, başlığı da yanıtta yok.
        var openLaw = await CreateLawAsync(admin, Visibility.Public);
        await CreateArticleAsync(admin, openLaw, "7", "Açık madde.");
        var secretTitle = "Gizli " + Guid.NewGuid().ToString("N")[..8];
        await CreateArticleAsync(admin, openLaw, "8", "Gizli madde.", visibility: Visibility.Private, title: secretTitle);
        var mixed = await CreateNoteAsync(admin, $"Karma not {Guid.NewGuid():N}"[..20], "<p>Bkz. (madde 7, 8).</p>", lawId: openLaw.Id);

        var mixedBody = await (await anonymous.GetAsync($"/api/lecture-notes/{mixed.Slug}")).Content.ReadAsStringAsync();
        Assert.Equal([$"{openLaw.Slug}/madde-7"], ReferencesIn(await GetNoteAsync(anonymous, mixed.Slug)));
        Assert.DoesNotContain($"{openLaw.Slug}/madde-8", mixedBody);
        Assert.DoesNotContain(secretTitle, mixedBody);
        Assert.Equal([$"{openLaw.Slug}/madde-7", $"{openLaw.Slug}/madde-8"], ReferencesIn(await GetNoteAsync(member, mixed.Slug)));
    }

    [Fact]
    public async Task LectureNote_HtmlUnchanged_ReadingHtmlMarked()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, Visibility.Public);
        await CreateArticleAsync(admin, law, "21", "Kast.");
        const string html = "<p>Kast (madde 21).</p>";
        var note = await CreateNoteAsync(admin, $"Ham {Guid.NewGuid():N}"[..16], html, lawId: law.Id);

        // Düzenleme formu kaydedilmiş HTML'i alır; işaret yalnız okuma HTML'indedir.
        var detail = await GetNoteAsync(admin, note.Slug);
        Assert.Equal(html, detail.Html);
        Assert.Contains($"data-article-ref=\"{law.Slug}/madde-21\"", detail.ReadingHtml);
        Assert.Contains("aria-label=\"", detail.ReadingHtml);
    }

    private static IEnumerable<string> ReferencesIn(LectureNoteDetail note) =>
        ArticleRefAttribute().Matches(note.ReadingHtml).Select(m => m.Groups[1].Value);

    [System.Text.RegularExpressions.GeneratedRegex("data-article-ref=\"([^\"]+)\"")]
    private static partial System.Text.RegularExpressions.Regex ArticleRefAttribute();

    private static async Task<LectureNoteDetail> GetNoteAsync(HttpClient client, string slug) =>
        await (await client.GetAsync($"/api/lecture-notes/{slug}")).ReadAsync<LectureNoteDetail>();

    private static async Task<LectureNoteDetail> CreateNoteAsync(
        HttpClient admin, string title, string html, Guid? lawId = null, Guid? groupId = null) =>
        await (await admin.PostJsonAsync("/api/admin/lecture-notes",
                new SaveLectureNoteRequest(title, html, Visibility.Public, groupId, lawId, null)))
            .ReadAsync<LectureNoteDetail>(201);

    private static async Task<LectureNoteDetail> AddHeadingAsync(HttpClient admin, Guid courseId, string title, string html, Guid? lawId)
    {
        var node = await (await admin.PostJsonAsync($"/api/admin/lecture-courses/{courseId}/nodes", new AddLectureNodeRequest(title, null, Visibility.Public)))
            .ReadAsync<LectureNoteDetail>(201);
        return await (await admin.PutJsonAsync($"/api/admin/lecture-notes/{node.Id}",
                new SaveLectureNoteRequest(title, html, Visibility.Public, null, lawId, null)))
            .ReadAsync<LectureNoteDetail>();
    }

    /// <summary>Kanunu belirsiz ders notunda atıfın kanunu (ArticleReferences.DefaultLawSlug).</summary>
    private const string TckSlug = "tck";

    /// <summary>Varsayılan kanun: adres adı "tck" olan kanun (tekil). Bu oturumda ilk kez gerekiyorsa kurulur.</summary>
    private static async Task<LawSummary> EnsureTckAsync(HttpClient admin)
    {
        var existing = await admin.GetAsync($"/api/library/laws/{TckSlug}");
        if (existing.IsSuccessStatusCode)
            return (await existing.ReadAsync<LawTreeResponse>()).Law;
        var tck = await CreateLawAsync(admin, Visibility.Public, TckSlug);
        await CreateArticleAsync(admin, tck, "21", "Kast.");
        return tck;
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
