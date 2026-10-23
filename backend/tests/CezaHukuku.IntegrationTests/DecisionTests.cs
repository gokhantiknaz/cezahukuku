using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class DecisionTests(ApiFactory factory)
{
    private static SaveDecisionRequest Ictihat(string caseNumber, Visibility visibility = Visibility.Public, List<Guid>? articles = null) =>
        new(Court.Yargitay, "Ceza Genel Kurulu", DecisionKind.Ictihat, caseNumber, "2020/1", new DateOnly(2020, 3, 12), null,
            null, null, "Tasarlama için kararın önceden verilmesi gerekir.", "<p>Tam metin <script>x</script>alıntısı.</p>",
            "https://karararama.yargitay.gov.tr/", visibility, articles);

    [Fact]
    public async Task Decision_LinkedToTwoArticles_ShowsInBoth_AndHtmlIsSanitized()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, m81, m82) = await CreateLawAsync(admin);

        var created = await (await admin.PostJsonAsync("/api/admin/decisions", Ictihat("2019/1-100", articles: [m81.Id, m82.Id])))
            .ReadAsync<AdminDecisionDetail>(201);
        Assert.Equal(["81", "82"], created.Articles.Select(a => a.Number));
        Assert.Equal("<p>Tam metin alıntısı.</p>", created.Decision.Html);

        var anonymous = factory.CreateAnonymousClient();
        foreach (var slug in (string[])["madde-81", "madde-82"])
        {
            var detail = await (await anonymous.GetAsync($"/api/library/laws/{law.Slug}/articles/{slug}")).ReadAsync<ArticleDetailResponse>();
            var decision = Assert.Single(detail.Decisions);
            Assert.Equal(("Ceza Genel Kurulu", "2019/1-100"), (decision.Chamber, decision.CaseNumber));
        }
    }

    [Fact]
    public async Task NormReview_RequiresConstitutionalCourtAndOutcome()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (_, m81, _) = await CreateLawAsync(admin);

        var wrongCourt = new SaveDecisionRequest(Court.Yargitay, null, DecisionKind.NormDenetimi, "2015/1", "2016/2", null,
            NormReviewOutcome.Iptal, null, null, "Özet", null, null, Visibility.Public, null);
        await admin.PostJsonAsync("/api/admin/decisions", wrongCourt).ExpectAsync(400);
        await admin.PostJsonAsync("/api/admin/decisions", wrongCourt with { Court = Court.AnayasaMahkemesi, Outcome = null }).ExpectAsync(400);

        var review = await (await admin.PostJsonAsync("/api/admin/decisions",
                wrongCourt with { Court = Court.AnayasaMahkemesi, GazetteDate = new DateOnly(2016, 5, 1), GazetteNumber = "29700", ArticleIds = [m81.Id] }))
            .ReadAsync<AdminDecisionDetail>(201);
        Assert.Equal((DecisionKind.NormDenetimi, NormReviewOutcome.Iptal), (review.Decision.Kind, review.Decision.Outcome));

        // İçtihatta sonuç tutulmaz.
        var ictihat = await (await admin.PostJsonAsync("/api/admin/decisions", Ictihat("2019/2") with { Outcome = NormReviewOutcome.Ret }))
            .ReadAsync<AdminDecisionDetail>(201);
        Assert.Null(ictihat.Decision.Outcome);
    }

    [Fact]
    public async Task PrivateDecision_AndDecisionOfPrivateArticle_AreHiddenFromAnonymous()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, m81, m82) = await CreateLawAsync(admin);
        await admin.PostJsonAsync("/api/admin/decisions", Ictihat("2019/3", Visibility.Private, [m81.Id])).ExpectAsync(201);
        await admin.PostJsonAsync("/api/admin/decisions", Ictihat("2019/4", Visibility.Public, [m81.Id, m82.Id])).ExpectAsync(201);

        var anonymous = factory.CreateAnonymousClient();
        var m81Anon = await (await anonymous.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81")).ReadAsync<ArticleDetailResponse>();
        Assert.Equal(["2019/4"], m81Anon.Decisions.Select(d => d.CaseNumber));
        var m81Admin = await (await admin.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81")).ReadAsync<ArticleDetailResponse>();
        Assert.Equal(2, m81Admin.Decisions.Count);

        await admin.PutJsonAsync($"/api/admin/articles/{m82.Id}",
            new SaveArticleRequest(null, "82", "Nitelikli haller", Visibility.Private, null)).ExpectAsync(200);
        var m82Admin = await (await admin.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-82")).ReadAsync<ArticleDetailResponse>();
        Assert.Equal(Visibility.Private, Assert.Single(m82Admin.Decisions).EffectiveVisibility);
    }

    [Fact]
    public async Task LinkUnlinkAndDelete()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, m81, m82) = await CreateLawAsync(admin);
        var created = await (await admin.PostJsonAsync("/api/admin/decisions", Ictihat("2019/5", articles: [m81.Id])))
            .ReadAsync<AdminDecisionDetail>(201);
        var id = created.Decision.Id;

        await admin.PutAsync($"/api/admin/articles/{m82.Id}/decisions/{id}", null).ExpectAsync(204);
        await admin.PutAsync($"/api/admin/articles/{m82.Id}/decisions/{id}", null).ExpectAsync(204); // ikinci kez: değişiklik yok
        var detail = await (await admin.GetAsync($"/api/admin/decisions/{id}")).ReadAsync<AdminDecisionDetail>();
        Assert.Equal(["81", "82"], detail.Articles.Select(a => a.Number));

        await admin.DeleteAsync($"/api/admin/articles/{m81.Id}/decisions/{id}").ExpectAsync(204);
        detail = await (await admin.GetAsync($"/api/admin/decisions/{id}")).ReadAsync<AdminDecisionDetail>();
        Assert.Equal(["82"], detail.Articles.Select(a => a.Number));

        await admin.DeleteAsync($"/api/admin/decisions/{id}").ExpectAsync(204);
        var m82Detail = await (await admin.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-82")).ReadAsync<ArticleDetailResponse>();
        Assert.Empty(m82Detail.Decisions);
    }

    [Fact]
    public async Task List_SearchesNumbersAndText()
    {
        var admin = await factory.CreateAdminClientAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        await admin.PostJsonAsync("/api/admin/decisions", Ictihat($"2021/{marker}")).ExpectAsync(201);

        var byNumber = await (await admin.GetAsync($"/api/admin/decisions?q={marker}")).ReadAsync<AdminDecisionList>();
        Assert.Equal(1, byNumber.Total);
        var byKind = await (await admin.GetAsync($"/api/admin/decisions?q={marker}&kind=normDenetimi")).ReadAsync<AdminDecisionList>();
        Assert.Equal(0, byKind.Total);

        await factory.CreateAnonymousClient().GetAsync("/api/admin/decisions").ExpectAsync(401);
    }

    private static async Task<(LawSummary Law, ArticleSummary M81, ArticleSummary M82)> CreateLawAsync(HttpClient admin)
    {
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups",
                new SaveLawGroupRequest("Kararlar", "t" + Guid.NewGuid().ToString("N")[..12], null)))
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
