using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Content;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Search;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Users;

namespace CezaHukuku.IntegrationTests;

/// <summary>Spec 0004: ders bölüm numarası ve başlıkları toplu içe aktarma.</summary>
[Collection(ApiCollection.Name)]
public class LectureImportTests(ApiFactory factory)
{
    private readonly string tag = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Aramada tek parça bulunan, bu teste özgü sözcük (yalnız harf).</summary>
    private string Word(string prefix) => prefix + new string([.. tag.Select(ch => (char)('k' + ch % 16))]);

    /// <summary>
    /// AC-1: bölüm numarası verilince numaralar her yerde onunla başlar (içindekiler, başlık sayfası, ders yolu,
    /// önceki/sonraki, madde ekranı, arama, taşıma yanıtı); kaldırılınca bugünkü biçime döner; adresler değişmez.
    /// </summary>
    [Fact]
    public async Task ChapterNumber_ChangesNumbersEverywhere_AndBack()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (law, article) = await CreateLawWithArticleAsync(admin);
        var title = $"Suç Teorisi {tag}";
        var course = await CreateCourseAsync(admin, title);
        var unsurlar = await AddNodeAsync(admin, course.Id, $"Suçun Unsurları {tag}");
        var word = Word("tipiklik");
        var tipiklik = await AddNodeAsync(admin, course.Id, $"Tipiklik {word}", unsurlar.Id);
        var sartlar = await AddNodeAsync(admin, course.Id, $"Şartlar {tag}");
        await admin.PutJsonAsync($"/api/admin/lecture-notes/{tipiklik.Id}",
            new SaveLectureNoteRequest(tipiklik.Title, tipiklik.Html, tipiklik.Visibility, null, null, article.Id)).ExpectAsync(200);
        var anonymous = factory.CreateAnonymousClient();

        async Task AssertNumbersAsync(int? chapter, string first, string child, string second)
        {
            var toc = await (await anonymous.GetAsync($"/api/lecture-courses/{course.Slug}")).ReadAsync<LectureCourseDetail>();
            Assert.Equal(chapter, toc.ChapterNumber);
            Assert.Equal([first, child, second], toc.Toc.Select(e => e.Label));
            var summary = (await (await anonymous.GetAsync("/api/lecture-courses")).ReadAsync<List<LectureCourseSummary>>())
                .Single(c => c.Id == course.Id);
            Assert.Equal(chapter, summary.ChapterNumber);

            var page = (await (await anonymous.GetAsync($"/api/lecture-notes/{tipiklik.Slug}")).ReadAsync<LectureNoteDetail>()).Node!;
            Assert.Equal((child, first, first, second),
                (page.Label, page.Path.Single().Label, page.Previous!.Label, page.Next!.Label));
            var articleRef = (await (await anonymous.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81"))
                .ReadAsync<ArticleDetailResponse>()).LectureNotes.Single(r => r.Slug == tipiklik.Slug);
            Assert.Equal((child, $"/ders-notlari/{course.Slug}/{tipiklik.Slug}"), (articleRef.Label, articleRef.Url));
            Assert.Equal($"{child} Tipiklik {word}", Assert.Single(await SearchAsync(anonymous, word)).Title);
        }

        await AssertNumbersAsync(null, "1. Bölüm", "1.1", "2. Bölüm");
        await SaveCourseAsync(admin, course.Id, title, 2).ExpectAsync(200);
        await AssertNumbersAsync(2, "2.1", "2.1.1", "2.2");

        // Taşıma yanıtı (bildirimdeki numara) da bölüm numarasıyla.
        var moved = await (await admin.PutJsonAsync($"/api/admin/lecture-notes/{sartlar.Id}/position",
            new MoveLectureNodeRequest(null, 0))).ReadAsync<LectureNoteDetail>();
        Assert.Equal("2.1", moved.Node!.Label);
        await admin.PutJsonAsync($"/api/admin/lecture-notes/{sartlar.Id}/position", new MoveLectureNodeRequest(null, 1)).ExpectAsync(200);

        await SaveCourseAsync(admin, course.Id, title, null).ExpectAsync(200);
        await AssertNumbersAsync(null, "1. Bölüm", "1.1", "2. Bölüm");

        // Sınırlar: 1 ve 99 geçer, 0 ve 100 geçmez.
        await SaveCourseAsync(admin, course.Id, title, 0).ExpectAsync(400);
        await SaveCourseAsync(admin, course.Id, title, 100).ExpectAsync(400);
        await SaveCourseAsync(admin, course.Id, title, 1).ExpectAsync(200);
        await SaveCourseAsync(admin, course.Id, title, 99).ExpectAsync(200);
    }

    private static Task<HttpResponseMessage> SaveCourseAsync(HttpClient admin, Guid id, string title, int? chapter) =>
        admin.PutJsonAsync($"/api/admin/lecture-courses/{id}", new SaveLectureCourseRequest(title, null, Visibility.Public, chapter));

    private static async Task<LectureCourseDetail> CreateCourseAsync(HttpClient admin, string title, Visibility visibility = Visibility.Public) =>
        await (await admin.PostJsonAsync("/api/admin/lecture-courses", new SaveLectureCourseRequest(title, null, visibility)))
            .ReadAsync<LectureCourseDetail>(201);

    private static async Task<LectureNoteDetail> AddNodeAsync(HttpClient admin, Guid courseId, string title, Guid? parentId = null) =>
        await (await admin.PostJsonAsync($"/api/admin/lecture-courses/{courseId}/nodes", new AddLectureNodeRequest(title, parentId, Visibility.Public)))
            .ReadAsync<LectureNoteDetail>(201);

    private static async Task<List<SearchHit>> SearchAsync(HttpClient client, string query) =>
        (await (await client.GetAsync($"/api/search?q={Uri.EscapeDataString(query)}&kinds=dersNotu&limit=50")).ReadAsync<SearchResponse>()).Hits;

    private static async Task<(LawSummary Law, ArticleSummary Article)> CreateLawWithArticleAsync(HttpClient admin)
    {
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Temel Ceza Kanunları", Slug(), null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", Slug(), null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
        var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, "81", "Kasten öldürme", Visibility.Public, null)))
            .ReadAsync<ArticleSummary>(201);
        return (law, article);
    }

    private async Task<HttpClient> CreateUserClientAsync(HttpClient admin, string role)
    {
        var email = $"{Slug()}@test.local";
        const string password = "TestKullanici123";
        await admin.PostJsonAsync("/api/admin/users", new CreateUserRequest(email, "Test Kullanıcı", role, password)).ExpectAsync(201);
        return await factory.CreateSignedInClientAsync(email, password);
    }

    private static string Slug() => "t" + Guid.NewGuid().ToString("N")[..12];
}
