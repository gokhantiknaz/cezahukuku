using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CitationTests(ApiFactory factory)
{
    [Fact]
    public async Task SameLawCitation_IsLinked_AndShownAsCitedBy_EvenWhenTargetAddedLater()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, "Deneme Kanunu");
        const string text = "Bir Türk vatandaşı, 13 üncü maddede yazılı suçlar dışında, Türkiye'de işlediği suçtan";
        await CreateArticleWithTextAsync(admin, law, "11", text);
        // Atıf yapılan madde sonradan giriliyor: bağlantı okuma anında kurulur.
        await CreateArticleWithTextAsync(admin, law, "13", "Bu Kanun, aşağıdaki suçların yabancı ülkede işlenmesi hâlinde uygulanır.");

        var m11 = await GetAsync(factory.CreateAnonymousClient(), law, "madde-11");
        var span = Assert.Single(m11.Citations);
        Assert.Equal(("13", "13 üncü maddede"), (span.Target.Number, text.Substring(span.Start, span.Length)));
        Assert.Equal(m11.Version!.Paragraphs.Single().Id, span.ParagraphId);

        var m13 = await GetAsync(factory.CreateAnonymousClient(), law, "madde-13");
        Assert.Equal(["11"], m13.CitedBy.Select(a => a.Number));
    }

    [Fact]
    public async Task CitationsToOtherLaw_ByNumberAndByName_AndInClauses()
    {
        var admin = await factory.CreateAdminClientAsync();
        var procedureName = $"{UniqueWord()} Muhakemesi Kanunu";
        var procedure = await CreateLawAsync(admin, procedureName);
        await CreateArticleWithTextAsync(admin, procedure, "171", "Kamu davasının açılmasının ertelenmesi.");
        await CreateArticleWithTextAsync(admin, procedure, "231", "Hükmün açıklanmasının geri bırakılması.");

        var penal = await CreateLawAsync(admin, "Deneme Ceza Kanunu");
        var paragraph = new ParagraphInput(null, 9,
            $"Bu maddede aksine düzenleme bulunmayan hâllerde, {procedureName.Replace("Kanunu", "Kanununun")} 171 inci maddesi uygulanır.",
            [new ParagraphItemInput(null, 1, "a)", $"{procedure.Number} sayılı Kanunun 231 inci maddesi,")]);
        await CreateArticleWithParagraphsAsync(admin, penal, "191", [paragraph]);

        var m191 = await GetAsync(admin, penal, "madde-191");
        Assert.Equal([("171", procedure.Slug, false), ("231", procedure.Slug, true)],
            m191.Citations.OrderBy(c => c.Target.Number).Select(c => (c.Target.Number, c.Target.LawSlug, c.ItemId is not null)));

        var m231 = await GetAsync(admin, procedure, "madde-231");
        Assert.Equal([(penal.Slug, "191")], m231.CitedBy.Select(a => (a.LawSlug, a.Number)));
    }

    [Fact]
    public async Task CitationToLawNotInLibrary_StaysPlainText()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, "Deneme Kanunu");
        await CreateArticleWithTextAsync(admin, law, "1", "31/8/1956 tarihli ve 6831 sayılı Orman Kanununun 108 inci maddesi saklıdır.");

        Assert.Empty((await GetAsync(admin, law, "madde-1")).Citations);
    }

    [Fact]
    public async Task CitationsFollowShownVersion_AndCitedByUsesVersionInForce()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, "Deneme Kanunu");
        var target = await CreateArticleWithTextAsync(admin, law, "5", "Hedef madde.");
        var source = await CreateArticleWithTextAsync(admin, law, "6", "5 inci madde hükümleri uygulanır.", new DateOnly(2005, 6, 1));
        // Yeni sürümde atıf kalkıyor.
        await admin.PostJsonAsync($"/api/admin/articles/{source.Id}/versions",
            new SaveArticleVersionRequest(new DateOnly(2020, 1, 1), null, null, null, null, false,
                [new ParagraphInput(null, 1, "Atıf kaldırıldı.", null)])).ExpectAsync(201);

        Assert.Empty((await GetAsync(admin, law, "madde-6")).Citations);
        Assert.Single((await GetAsync(admin, law, "madde-6?asOf=2010-01-01")).Citations);
        Assert.Empty((await GetAsync(admin, law, "madde-5")).CitedBy);
        Assert.NotNull(target);
    }

    [Fact]
    public async Task Rebuild_ReindexesAllVersions()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateLawAsync(admin, "Deneme Kanunu");
        await CreateArticleWithTextAsync(admin, law, "1", "Birinci madde.");
        await CreateArticleWithTextAsync(admin, law, "2", "1 inci ve 3 üncü maddeler saklıdır.");

        var count = await (await admin.PostAsync("/api/admin/citations/rebuild", null)).ReadAsync<int>();

        Assert.True(count >= 2);
        // 3. madde kütüphanede yok: yalnız 1'e atıf bağlanır.
        Assert.Equal(["1"], (await GetAsync(admin, law, "madde-2")).Citations.Select(c => c.Target.Number));
    }

    private static int lawNumber = Random.Shared.Next(50_000, 60_000);

    /// <summary>Gerçekçi (5 haneli) ve test oturumunda tekil kanun numarası: atıflar numarayla çözülür.</summary>
    private static string NextLawNumber() => Interlocked.Increment(ref lawNumber).ToString();

    private static string UniqueWord() =>
        "T" + new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(8).ToArray()).PadRight(8, 'x');

    private static async Task<ArticleDetailResponse> GetAsync(HttpClient client, LawSummary law, string slugAndQuery) =>
        await (await client.GetAsync($"/api/library/laws/{law.Slug}/articles/{slugAndQuery}")).ReadAsync<ArticleDetailResponse>();

    private static async Task<LawSummary> CreateLawAsync(HttpClient admin, string name)
    {
        var unique = Guid.NewGuid().ToString("N")[..10];
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Atıflar", "t" + unique, null)))
            .ReadAsync<LawGroupSummary>(201);
        return await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, NextLawNumber(), name, null, "t" + unique,
                    null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
    }

    private static Task<ArticleSummary> CreateArticleWithTextAsync(
        HttpClient admin, LawSummary law, string number, string text, DateOnly? validFrom = null) =>
        CreateArticleWithParagraphsAsync(admin, law, number, [new ParagraphInput(null, 1, text, null)], validFrom);

    private static async Task<ArticleSummary> CreateArticleWithParagraphsAsync(
        HttpClient admin, LawSummary law, string number, List<ParagraphInput> paragraphs, DateOnly? validFrom = null)
    {
        var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, number, $"Madde {number}", Visibility.Public, null)))
            .ReadAsync<ArticleSummary>(201);
        await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
            new SaveArticleVersionRequest(validFrom, null, null, null, null, false, paragraphs)).ExpectAsync(201);
        return article;
    }
}
