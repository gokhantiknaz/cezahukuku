using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Content;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Personal;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Users;

namespace CezaHukuku.IntegrationTests;

/// <summary>Ek modüller (M7): favoriler, Ceza Gündemi, Kendini Test Et.</summary>
[Collection(ApiCollection.Name)]
public class ExtrasTests(ApiFactory factory)
{
    [Fact]
    public async Task Favorites_AddListRemove_AreIdempotentAndPrivate()
    {
        var (admin, law, articles) = await CreateLawAsync();
        var user = await CreateUserClientAsync(admin, Roles.Standard);
        var other = await CreateUserClientAsync(admin, Roles.Standard);

        await user.PutAsync($"/api/me/favorites/{articles[0].Id}", null).ExpectAsync(204);
        await user.PutAsync($"/api/me/favorites/{articles[0].Id}", null).ExpectAsync(204); // ikinci kez: değişiklik yok
        await user.PutAsync($"/api/me/favorites/{articles[1].Id}", null).ExpectAsync(204);
        await user.PutAsync($"/api/me/favorites/{Guid.NewGuid()}", null).ExpectAsync(404);

        var favorites = await (await user.GetAsync("/api/me/favorites")).ReadAsync<List<FavoriteDto>>();
        Assert.Equal(["82", "81"], favorites.Select(f => f.Article.Number));
        Assert.All(favorites, f => Assert.Equal(law.Slug, f.Article.LawSlug));
        Assert.Empty(await (await other.GetAsync("/api/me/favorites")).ReadAsync<List<FavoriteDto>>());

        await user.DeleteAsync($"/api/me/favorites/{articles[0].Id}").ExpectAsync(204);
        await user.DeleteAsync($"/api/me/favorites/{articles[0].Id}").ExpectAsync(204);
        Assert.Equal(["82"], (await (await user.GetAsync("/api/me/favorites")).ReadAsync<List<FavoriteDto>>()).Select(f => f.Article.Number));

        var reader = await CreateUserClientAsync(admin, Roles.Reader);
        await reader.PutAsync($"/api/me/favorites/{articles[0].Id}", null).ExpectAsync(403);
    }

    [Fact]
    public async Task News_PublishedOnly_PrivateForMembers_AdminManages()
    {
        var admin = await factory.CreateAdminClientAsync();
        var title = $"Haber {Guid.NewGuid():N}";
        var today = DateOnly.FromDateTime(DateTime.Now);
        var created = await (await admin.PostJsonAsync("/api/admin/news",
                new SaveNewsItemRequest($"{title} yayında", "Özet.", "https://www.resmigazete.gov.tr", today, Visibility.Public)))
            .ReadAsync<NewsItemDto>(201);
        await admin.PostJsonAsync("/api/admin/news",
            new SaveNewsItemRequest($"{title} üyelere", null, null, today, Visibility.Private)).ExpectAsync(201);
        await admin.PostJsonAsync("/api/admin/news",
            new SaveNewsItemRequest($"{title} ileride", null, null, today.AddDays(3), Visibility.Public)).ExpectAsync(201);
        await admin.PostJsonAsync("/api/admin/news", new SaveNewsItemRequest(" ", null, "javascript:alert(1)", today, Visibility.Public))
            .ExpectAsync(400);

        var anonymous = await (await factory.CreateAnonymousClient().GetAsync("/api/news?limit=100")).ReadAsync<List<NewsItemDto>>();
        Assert.Equal([$"{title} yayında"], anonymous.Where(n => n.Title.StartsWith(title)).Select(n => n.Title));
        var member = await CreateUserClientAsync(admin, Roles.Reader);
        Assert.Equal(2, (await (await member.GetAsync("/api/news?limit=100")).ReadAsync<List<NewsItemDto>>()).Count(n => n.Title.StartsWith(title)));

        await factory.CreateAnonymousClient().PostJsonAsync("/api/admin/news",
            new SaveNewsItemRequest("x", null, null, today, Visibility.Public)).ExpectAsync(401);
        await admin.DeleteAsync($"/api/admin/news/{created.Id}").ExpectAsync(204);
        Assert.DoesNotContain(await (await factory.CreateAnonymousClient().GetAsync("/api/news?limit=100")).ReadAsync<List<NewsItemDto>>(),
            n => n.Id == created.Id);
    }

    [Fact]
    public async Task Quiz_RandomByLaw_ValidatesAndHidesPrivate()
    {
        var (admin, law, articles) = await CreateLawAsync();
        var question = await (await admin.PostJsonAsync("/api/admin/quiz",
                new SaveQuizQuestionRequest(law.Id, articles[0].Id, "Kasten öldürmenin cezası nedir?",
                    ["Müebbet hapis", "Ağırlaştırılmış müebbet hapis", "10 yıl hapis"], 0, "TCK 81: müebbet hapis.", Visibility.Public)))
            .ReadAsync<QuizQuestionDto>(201);
        Assert.Equal(("81", "TCK"), (question.Article!.Number, question.LawLabel));
        await admin.PostJsonAsync("/api/admin/quiz",
            new SaveQuizQuestionRequest(law.Id, null, "Üyelere özel soru?", ["Evet", "Hayır"], 1, null, Visibility.Private)).ExpectAsync(201);

        // Geçersiz: tek şık, doğru şık aralık dışı, başka kanunun maddesi.
        await admin.PostJsonAsync("/api/admin/quiz", new SaveQuizQuestionRequest(law.Id, null, "Soru?", ["Tek"], 0, null, Visibility.Public)).ExpectAsync(400);
        await admin.PostJsonAsync("/api/admin/quiz", new SaveQuizQuestionRequest(law.Id, null, "Soru?", ["A", "B"], 2, null, Visibility.Public)).ExpectAsync(400);
        var (_, otherLaw, _) = await CreateLawAsync();
        await admin.PostJsonAsync("/api/admin/quiz",
            new SaveQuizQuestionRequest(otherLaw.Id, articles[0].Id, "Soru?", ["A", "B"], 0, null, Visibility.Public)).ExpectAsync(400);

        var anonymous = await (await factory.CreateAnonymousClient().GetAsync($"/api/quiz?law={law.Slug}&count=10")).ReadAsync<List<QuizQuestionDto>>();
        Assert.Equal([question.Id], anonymous.Select(q => q.Id));
        var member = await CreateUserClientAsync(admin, Roles.Reader);
        Assert.Equal(2, (await (await member.GetAsync($"/api/quiz?law={law.Slug}")).ReadAsync<List<QuizQuestionDto>>()).Count);

        // Madde private olunca ona bağlı soru anonimden gizlenir.
        await admin.PutJsonAsync($"/api/admin/articles/{articles[0].Id}",
            new SaveArticleRequest(null, "81", "Kasten öldürme", Visibility.Private, null)).ExpectAsync(200);
        Assert.Empty(await (await factory.CreateAnonymousClient().GetAsync($"/api/quiz?law={law.Slug}")).ReadAsync<List<QuizQuestionDto>>());

        await admin.DeleteAsync($"/api/admin/quiz/{question.Id}").ExpectAsync(204);
        Assert.Single(await (await admin.GetAsync($"/api/admin/quiz?lawId={law.Id}")).ReadAsync<List<QuizQuestionDto>>());
    }

    private async Task<(HttpClient Admin, LawSummary Law, List<ArticleSummary> Articles)> CreateLawAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Ek", Slug(), null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", Slug(), null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
        var articles = new List<ArticleSummary>();
        foreach (var (number, title) in new[] { ("81", "Kasten öldürme"), ("82", "Nitelikli haller") })
        {
            articles.Add(await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                    new SaveArticleRequest(null, number, title, Visibility.Public, null)))
                .ReadAsync<ArticleSummary>(201));
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
