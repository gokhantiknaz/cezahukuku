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
    public async Task Import_IntoLawWithContent_IsRejected()
    {
        var admin = await factory.CreateAdminClientAsync();
        var law = await CreateEmptyLawAsync(admin, "5237");
        var request = new ImportLawTextRequest(TckExcerpt, false, null, false);
        await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import", request).ExpectAsync(200);

        var preview = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import/preview", request))
            .ReadAsync<LawImportPreview>();
        Assert.Contains(preview.Blockers, b => b.Contains("yalnız boş kanuna"));
        await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/import", request).ExpectAsync(400);
    }

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
