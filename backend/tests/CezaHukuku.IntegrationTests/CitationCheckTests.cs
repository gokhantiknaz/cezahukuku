using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Tools;
using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Parsing.Decisions;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CitationCheckTests(ApiFactory factory)
{
    /// <summary>
    /// Deneme kanunu (kısaltması her testte farklı; ortak veritabanındaki öbür "TCK"lerle karışmasın):
    /// 81 iki fıkralı, 2014'te değişti; 82 2016'da mülga; 83 AYM'ce iptal edildi.
    /// </summary>
    private async Task<Library> CreateLibraryAsync(HttpClient admin)
    {
        var abbreviation = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHJKLMNPRSTUVYZ"[Random.Shared.Next(21)]).ToArray());
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups",
                new SaveLawGroupRequest("Denetim", "t" + Guid.NewGuid().ToString("N")[..12], null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Denetim Kanunu", abbreviation, "t" + Guid.NewGuid().ToString("N")[..12],
                    null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);

        var m81 = await ArticleAsync(admin, law, "81", new DateOnly(2005, 6, 1),
            [
                new ParagraphInput(null, 1, "Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.", null),
                new ParagraphInput(null, 2, "Kasten öldürme suçunun;", [new(null, 1, "a)", "Tasarlayarak,"), new(null, 1, "b)", "Canavarca hisle,")]),
            ]);
        await admin.PostJsonAsync($"/api/admin/articles/{m81.Id}/versions",
            new SaveArticleVersionRequest(new DateOnly(2014, 1, 1), null, "6545 s.K. m.1", null, null, false,
                [new ParagraphInput(null, 1, "Bir insanı kasten öldüren kişi, ağırlaştırılmış müebbet hapis cezası ile cezalandırılır.", null),
                 new ParagraphInput(null, 2, "Kasten öldürme suçunun;", [new(null, 1, "a)", "Tasarlayarak,"), new(null, 1, "b)", "Canavarca hisle,")])]))
            .ExpectAsync(201);

        var m82 = await ArticleAsync(admin, law, "82", new DateOnly(2005, 6, 1), [new ParagraphInput(null, null, "Eski metin.", null)]);
        await admin.PostJsonAsync($"/api/admin/articles/{m82.Id}/versions",
            new SaveArticleVersionRequest(new DateOnly(2016, 1, 1), null, "6789 s.K. m.2", null, null, true, [])).ExpectAsync(201);

        var m83 = await ArticleAsync(admin, law, "83", new DateOnly(2005, 6, 1), [new ParagraphInput(null, null, "İptal edilen metin.", null)]);
        await admin.PostJsonAsync("/api/admin/decisions",
            new SaveDecisionRequest(Court.AnayasaMahkemesi, null, DecisionKind.NormDenetimi, $"2015/{Unique()}", $"2016/{Unique()}",
                new DateOnly(2016, 5, 1), NormReviewOutcome.Iptal, null, null, "İptal", null, null, Visibility.Public, [m83.Id]))
            .ExpectAsync(201);

        return new Library(abbreviation, law, m81);
    }

    private sealed record Library(string Abbreviation, LawSummary Law, ArticleSummary M81);

    private static int Unique() => Random.Shared.Next(100_000, 999_999);

    private static async Task<ArticleSummary> ArticleAsync(
        HttpClient admin, LawSummary law, string number, DateOnly validFrom, List<ParagraphInput> paragraphs)
    {
        var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, number, $"Madde {number}", Visibility.Public, null))).ReadAsync<ArticleSummary>(201);
        await admin.PostJsonAsync($"/api/admin/articles/{article.Id}/versions",
            new SaveArticleVersionRequest(validFrom, null, null, null, null, false, paragraphs)).ExpectAsync(201);
        return article;
    }

    private static async Task<CitationCheckResult> CheckAsync(HttpClient client, string text, DateOnly? referenceDate = null) =>
        await (await client.PostJsonAsync("/api/tools/citation-check", new CitationCheckRequest(text, referenceDate)))
            .ReadAsync<CitationCheckResult>();

    private static IEnumerable<string> Codes(CheckedArticleCitation citation) => citation.Issues.Select(i => i.Code);

    [Fact]
    public async Task Articles_AgainstCurrentText()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (x, law, _) = await CreateLibraryAsync(admin);

        var result = await CheckAsync(factory.CreateAnonymousClient(),
            $"Sanığa {x} 81/2-b uygulanmış; {x} m. 82, {x} 83, {x} 84, {x} 81/3 ve {x} 81/2-e de anılmış. KDV 18 ise atıf değil.");

        Assert.Equal(
            [$"{x} m. 81/2-b", $"{x} m. 82", $"{x} m. 83", $"{x} 84", $"{x} m. 81/3", $"{x} m. 81/2-e"],
            result.Articles.Select(a => a.Label));
        var (m81, m82, m83, m84, m81p3, m81e) = (result.Articles[0], result.Articles[1], result.Articles[2], result.Articles[3],
            result.Articles[4], result.Articles[5]);

        Assert.Equal(["degisiklik"], Codes(m81));
        Assert.Equal(CheckSeverity.Info, m81.Severity);
        Assert.Equal(["mulga"], Codes(m82));
        Assert.Equal(["aym_iptal"], Codes(m83));
        Assert.EndsWith("?sekme=norm", m83.Issues[0].Url);
        Assert.Equal((CheckSeverity.Error, "madde_yok"), (m84.Severity, Assert.Single(m84.Issues).Code));
        Assert.Contains("fikra_yok", Codes(m81p3));
        Assert.Contains("maddede 2 fıkra var", m81p3.Issues.Single(i => i.Code == "fikra_yok").Message);
        Assert.Contains("bent_yok", Codes(m81e));
        Assert.Equal((law.Slug, "madde-81"), (m81.Article!.LawSlug, m81.Article.ArticleSlug));
    }

    [Fact]
    public async Task Articles_AtReferenceDate()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (x, law, _) = await CreateLibraryAsync(admin);
        var client = factory.CreateAnonymousClient();

        var in2010 = await CheckAsync(client, $"{x} 81 ve {x} 82", new DateOnly(2010, 5, 1));
        Assert.All(in2010.Articles, a => Assert.Equal(["sonradan_degisti"], Codes(a)));
        Assert.Equal($"/{law.Slug}/madde-81?tarih=2010-05-01", in2010.Articles[0].Issues[0].Url);
        Assert.Contains("mülga", in2010.Articles[1].Issues[0].Message);

        var in2020 = await CheckAsync(client, $"{x} 81 ve {x} 82", new DateOnly(2020, 5, 1));
        Assert.Equal(["yururlukte"], Codes(in2020.Articles[0]));
        Assert.Equal(["mulga_tarihte"], Codes(in2020.Articles[1]));

        var in2004 = await CheckAsync(client, $"{x} 81", new DateOnly(2004, 1, 1));
        Assert.Equal(["yururlukte_degil"], Codes(Assert.Single(in2004.Articles)));
    }

    [Fact]
    public async Task Decisions_FoundSimilarAndUnverified()
    {
        var admin = await factory.CreateAdminClientAsync();
        var library = await CreateLibraryAsync(admin);
        var (e, k) = (Unique(), Unique());
        await admin.PostJsonAsync("/api/admin/decisions",
            new SaveDecisionRequest(Court.Yargitay, "1. Ceza Dairesi", DecisionKind.Ictihat, $"2018/{e}", $"2019/{k}",
                new DateOnly(2019, 3, 12), null, null, null, "Özet", null, null, Visibility.Public, [library.M81.Id])).ExpectAsync(201);
        // 81. madde 2014'te değişti: 2012 tarihli karar eskimiş olabilir.
        var (oldE, oldK) = (Unique(), Unique());
        await admin.PostJsonAsync("/api/admin/decisions",
            new SaveDecisionRequest(Court.Yargitay, "1. CD", DecisionKind.Ictihat, $"2011/{oldE}", $"2012/{oldK}",
                new DateOnly(2012, 6, 1), null, null, null, "Özet", null, null, Visibility.Public, [library.M81.Id])).ExpectAsync(201);

        var result = await CheckAsync(factory.CreateAnonymousClient(), string.Join("\n",
            $"Yargıtay 1. CD, 12.03.2019, 2018/{e} E., 2019/{k} K.",
            $"Yargıtay 1. Ceza Dairesinin 13.03.2019 tarihli ve 2018/{e} E., 2019/{k} K. sayılı kararı",
            $"(Y. 1. CD, E. 2018/{e}, K. 2019/{k + 1}, T. 12.03.2019)",
            $"Yargıtay 3. CD, 12.03.2019, 2018/{Unique()} E., 2019/{Unique()} K.",
            $"Yargıtay 1. CD, 12.03.2020, 2018/{Unique()} E., 2019/{Unique()} K.",
            $"YCGK 01.06.2012, 2011/{oldE} E., 2012/{oldK} K."));

        Assert.Equal(6, result.Decisions.Count);
        Assert.Empty(result.Articles); // künyenin içindeki "YCGK 2011/…" madde atıfı sayılmaz
        var (found, wrongDate, similar, unknown, inconsistent, old) = (result.Decisions[0], result.Decisions[1], result.Decisions[2],
            result.Decisions[3], result.Decisions[4], result.Decisions[5]);

        Assert.Equal((CheckSeverity.Ok, "bulundu"), (found.Severity, Assert.Single(found.Issues).Code));
        Assert.Equal($"/{library.Law.Slug}/madde-81?sekme=ictihatlar", found.Match?.Url);
        Assert.Equal($"Yargıtay 1. Ceza Dairesi, 12.03.2019, E. 2018/{e}, K. 2019/{k}", found.Label);
        Assert.Equal(found.Key, wrongDate.Key);

        Assert.Equal(["bulundu", "tarih_farkli"], wrongDate.Issues.Select(i => i.Code));
        Assert.Equal((CheckSeverity.Warning, "benzer"), (similar.Severity, Assert.Single(similar.Issues).Code));
        Assert.Contains($"karar numarası 2019/{k}", similar.Issues[0].Message);

        Assert.Equal("dogrulanacak", Assert.Single(unknown.Issues).Code);
        Assert.Equal("https://karararama.yargitay.gov.tr/", unknown.Issues[0].Url);
        Assert.Equal(["karar_yili", "dogrulanacak"], inconsistent.Issues.Select(i => i.Code));
        Assert.Equal(CheckSeverity.Error, inconsistent.Severity);

        // YCGK künyesi "1. CD" kaydıyla çelişiyor; esas ve karar tuttuğu için yine bulunur, daire farkı bildirilir.
        Assert.Equal(["bulundu", "daire_farkli", "karardan_sonra_degisti"], old.Issues.Select(i => i.Code));
        Assert.Contains($"{library.Abbreviation} m. 81 değişti (6545 s.K. m.1, 01.01.2014)", old.Issues[2].Message);
    }

    [Fact]
    public async Task PrivateDecision_IsOnlyFoundBySignedInUsers()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (e, k) = (Unique(), Unique());
        await admin.PostJsonAsync("/api/admin/decisions",
            new SaveDecisionRequest(Court.Yargitay, "2. Ceza Dairesi", DecisionKind.Ictihat, $"2018/{e}", $"2019/{k}",
                new DateOnly(2019, 3, 12), null, null, null, "Özet", null, null, Visibility.Private, null)).ExpectAsync(201);
        var text = $"Yargıtay 2. CD, 12.03.2019, 2018/{e} E., 2019/{k} K.";

        var anonymous = await CheckAsync(factory.CreateAnonymousClient(), text);
        Assert.Equal("dogrulanacak", Assert.Single(Assert.Single(anonymous.Decisions).Issues).Code);
        var signedIn = await CheckAsync(admin, text);
        Assert.Equal("bulundu", Assert.Single(Assert.Single(signedIn.Decisions).Issues).Code);
    }

    [Fact]
    public async Task Validation_AndFileUploadNeedsSignIn()
    {
        var anonymous = factory.CreateAnonymousClient();
        await anonymous.PostJsonAsync("/api/tools/citation-check", new CitationCheckRequest("  ", null)).ExpectAsync(400);
        await anonymous.PostJsonAsync("/api/tools/citation-check",
            new CitationCheckRequest(new string('a', CitationCheckEndpoints.MaxTextLength + 1), null)).ExpectAsync(400);

        using var form = new MultipartFormDataContent { { new ByteArrayContent("TCK 81"u8.ToArray()), "file", "metin.txt" } };
        Assert.Equal(401, (int)(await anonymous.PostAsync("/api/tools/extract-text", form)).StatusCode);
        var admin = await factory.CreateAdminClientAsync();
        using var form2 = new MultipartFormDataContent { { new ByteArrayContent("TCK 81"u8.ToArray()), "file", "metin.txt" } };
        Assert.Equal("TCK 81", (await (await admin.PostAsync("/api/tools/extract-text", form2)).ReadAsync<ExtractedText>()).Text);
    }
}
