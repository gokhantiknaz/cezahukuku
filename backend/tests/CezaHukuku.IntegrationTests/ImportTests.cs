using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ImportTests(ApiFactory factory)
{
    private static readonly string TckExcerpt =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tck-kesit.txt"));

    [Fact]
    public async Task Import_CreatesTreeArticlesAndFirstVersions_AsPreviewed()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateEmptyLawAsync(admin, "5237");
        var request = new ImportLawTextRequest(TckExcerpt, false, new DateOnly(2005, 6, 1), ApplyMetadata: true);

        var preview = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import/preview", request))
            .ReadAsync<LawImportPreview>();
        Assert.Empty(preview.Blockers);
        Assert.Equal((14, 1), (preview.ArticleCount, preview.RepealedCount));

        var result = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import", request)).ReadAsync<LawImportResult>();
        Assert.Equal((preview.NodeCount, preview.ArticleCount), (result.NodeCount, result.ArticleCount));

        var tree = await (await admin.GetAsync($"/api/library/laws/{law.Slug}")).ReadAsync<LawTreeResponse>();
        Assert.Equal(["Birinci Kitap", "İkinci Kitap"], tree.Nodes.Select(n => n.Number));
        Assert.Equal(new DateOnly(2004, 10, 12), tree.Law.GazetteDate);

        var m82 = await (await admin.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-82")).ReadAsync<ArticleDetailResponse>();
        Assert.Equal("Nitelikli haller", m82.Article.Title);
        Assert.Equal(["Özel Hükümler", "Kişilere Karşı Suçlar", "Hayata Karşı Suçlar"], m82.Path.Select(p => p.Title));
        Assert.Equal(12, m82.Version?.Paragraphs.Single().Items.Count);
        Assert.Equal("madde-83", m82.Next?.Slug);

        // Değişmemiş madde girilen yürürlük tarihiyle, değişmiş madde son değişiklik notunun tarihiyle başlar:
        // 2010'da işlenen bir suç için 82'nin 2022'de değişen bugünkü metni gösterilmemeli.
        var m81 = await (await admin.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81")).ReadAsync<ArticleDetailResponse>();
        Assert.Equal(new DateOnly(2005, 6, 1), m81.Version?.ValidFrom);
        Assert.Equal(new DateOnly(2022, 5, 12), m82.Version?.ValidFrom);
        Assert.Equal("Değişik:12/5/2022-7406/2 md.", m82.Version?.AmendedBy);
        var m82In2010 = await (await admin.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-82?asOf=2010-01-01"))
            .ReadAsync<ArticleDetailResponse>();
        Assert.Null(m82In2010.Version);
        Assert.Contains(preview.Warnings, w => w.Contains("değişiklik notu"));

        var temporary = await (await admin.GetAsync($"/api/library/laws/{law.Slug}/articles/gecici-madde-1"))
            .ReadAsync<ArticleDetailResponse>();
        Assert.Equal("Dokuzuncu Bölüm", temporary.Path[^1].Number);

        var repealed = await (await admin.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-18")).ReadAsync<ArticleDetailResponse>();
        Assert.True(repealed.Version?.IsRepealed);
    }

    [Fact]
    public async Task Reimport_OfSameText_ChangesNothing()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateEmptyLawAsync(admin, "5237");
        var request = new ImportLawTextRequest(TckExcerpt, false, new DateOnly(2005, 6, 1), false);
        await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import", request).ExpectAsync(200);

        var preview = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import/preview", request))
            .ReadAsync<LawImportPreview>();

        var update = Assert.IsType<LawUpdatePreview>(preview.Update);
        Assert.Empty(update.Changes);
        Assert.Equal(14, update.UnchangedCount);
        Assert.Equal(0, update.NewNodeCount);
        Assert.Empty(update.NotInText);
    }

    [Fact]
    public async Task Reimport_OfAmendedText_AddsVersionsCorrectsAndReports()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateEmptyLawAsync(admin, "5237");
        await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import",
            new ImportLawTextRequest(TckExcerpt, false, new DateOnly(2005, 6, 1), false)).ExpectAsync(200);
        var before81 = await GetArticleAsync(admin, law, "madde-81");

        // Kanunun "yeni" hâli: 81 değişiklik notuyla değişti, 83'te notsuz yazım düzeltmesi, 345'in başlığı değişti,
        // 343 kaldırıldı, sona yeni bir madde ve yeni bir bölüm eklendi.
        var amended = TckExcerpt
            .Replace("Madde 81- (1) Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.",
                "Madde 81- (1) (Değişik: 1/1/2026-9999/1 md.) Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.")
            .Replace("(2) İhmali ve icrai davranışın eşdeğer kabul edilebilmesi için, kişinin;",
                "(2) İhmali ve icrai davranışın eşdeğer kabul edilebilmesi için kişinin;")
            .Replace("Karşılıklılık koşulu\nMadde 343- (1) Bu bölümde yazılı hükümlerin uygulanması, karşılıklılık koşuluna bağlıdır.\n", "")
            .Replace("Yürütme\nMadde 345-", "Yürütme hükmü\nMadde 345-")
            .Replace("5237 SAYILI KANUNA İŞLENEMEYEN HÜKÜMLER",
                "ONUNCU BÖLÜM\nEk Hükümler\nDeneme maddesi\nMadde 346- (1) (Ek: 2/2/2026-9999/2 md.) Yeni hüküm.\n5237 SAYILI KANUNA İŞLENEMEYEN HÜKÜMLER");
        var request = new ImportLawTextRequest(amended, false, new DateOnly(2005, 6, 1), false);

        var preview = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import/preview", request))
            .ReadAsync<LawImportPreview>();
        var update = Assert.IsType<LawUpdatePreview>(preview.Update);
        var changes = update.Changes.ToDictionary(c => c.Number);

        Assert.Equal(["81", "83", "345", "346"], update.Changes.Select(c => c.Number)); // kanundaki sırasıyla
        Assert.True(changes["81"].AsNewVersion);
        Assert.Equal(new DateOnly(2026, 1, 1), changes["81"].ValidFrom);
        Assert.False(changes["83"].AsNewVersion); // notsuz fark: yerinde düzeltme
        Assert.Equal(("Yürütme", false), (changes["345"].OldTitle, changes["345"].TextChanged));
        Assert.True(changes["346"].IsNew);
        Assert.Equal(1, update.NewNodeCount);
        Assert.Equal(["343"], update.NotInText);

        var result = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import", request)).ReadAsync<LawImportResult>();
        Assert.Equal((true, 4), (result.IsUpdate, result.ArticleCount));

        // 81: yeni sürüm; 2010 tarihli soru eski metni, bugün yeni metni gösterir; fıkra kimliği korunur.
        var after81 = await GetArticleAsync(admin, law, "madde-81");
        Assert.Equal(2, after81.Versions.Count);
        Assert.StartsWith("(Değişik: 1/1/2026", after81.Version?.Paragraphs.Single().Text);
        Assert.Equal(before81.Version?.Paragraphs.Single().Id, after81.Version?.Paragraphs.Single().Id);
        var in2010 = await GetArticleAsync(admin, law, "madde-81?asOf=2010-03-15");
        Assert.Equal(before81.Version?.Paragraphs.Single().Text, in2010.Version?.Paragraphs.Single().Text);

        // 83: tek sürüm, düzeltilmiş metin.
        var m83 = await GetArticleAsync(admin, law, "madde-83");
        Assert.Single(m83.Versions);
        Assert.Equal("İhmali ve icrai davranışın eşdeğer kabul edilebilmesi için kişinin;", m83.Version?.Paragraphs[1].Text);

        // 343 silinmedi; 346 yeni bölümde.
        await admin.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-343").ExpectAsync(200);
        var m346 = await GetArticleAsync(admin, law, "madde-346");
        Assert.Equal("Onuncu Bölüm", m346.Path[^1].Number);
        Assert.Equal("Yürütme hükmü", (await GetArticleAsync(admin, law, "madde-345")).Article.Title);

        // Aynı metni tekrar yapıştırmak artık değişiklik üretmez.
        var again = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import/preview", request))
            .ReadAsync<LawImportPreview>();
        Assert.Empty(again.Update!.Changes);
    }

    private static async Task<ArticleDetailResponse> GetArticleAsync(HttpClient client, LawSummary law, string slugAndQuery) =>
        await (await client.GetAsync($"/api/library/laws/{law.Slug}/articles/{slugAndQuery}")).ReadAsync<ArticleDetailResponse>();

    [Fact]
    public async Task Preview_WarnsWhenLawNumberDiffers_AndBlocksWhenNoArticles()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateEmptyLawAsync(admin, "5271");

        var preview = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import/preview",
                new ImportLawTextRequest(TckExcerpt, false, null, false)))
            .ReadAsync<LawImportPreview>();
        Assert.Contains(preview.Warnings, w => w.Contains("(5237)") && w.Contains("(5271)"));

        var empty = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import/preview",
                new ImportLawTextRequest("Bu bir kanun metni değil.", false, null, false)))
            .ReadAsync<LawImportPreview>();
        Assert.Contains(empty.Blockers, b => b.Contains("madde bulunamadı"));
    }

    private static async Task<LawSummary> CreateEmptyLawAsync(HttpClient admin, string number)
    {
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups",
                new SaveLawGroupRequest("İçe aktarma", "t" + Guid.NewGuid().ToString("N")[..12], null)))
            .ReadAsync<LawGroupSummary>(201);
        return await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, number, "Deneme", null, "t" + Guid.NewGuid().ToString("N")[..12],
                    null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
    }
}
