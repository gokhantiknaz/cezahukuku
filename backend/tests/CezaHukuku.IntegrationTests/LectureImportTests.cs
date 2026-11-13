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

    private string BookChapter(string word) => $"""
        CEZA HUKUKU DERS NOTLARI
        2
        2. BÖLÜM – SUÇ TEORİSİ
        2.1. Suçun Unsurları
        2.1.1. Tipiklik {word}
        2.1.2. Hukuka Aykırılık
        2.2. Cezalandırılabilirliğin Diğer
        Şartları
        2.9. İçtima
        2.9.1. Genel Olarak
        2.9.3. Genel Olarak
        """;

    /// <summary>
    /// AC-6 (+ AC-2/AC-5 sunucu yolu): önizleme önerileri doldurur ve hiçbir şey kaydetmez; kayıt yeni dersi listenin
    /// sonunda, başlıkları metindeki sırayla ve önizlemedeki numaralarla kurar.
    /// </summary>
    [Fact]
    public async Task Import_CreatesCourseAtEnd_WithAllHeadingsInOrder()
    {
        var admin = await factory.CreateAdminClientAsync();
        var text = BookChapter(Word("tipik"));
        var before = await CoursesAsync(admin);

        var preview = await PreviewAsync(admin, new(text, null, null, ApplySuggestions: true));
        Assert.Equal(("Suç Teorisi", 2), (preview.Title, preview.ChapterNumber));
        Assert.Empty(preview.Errors);
        Assert.Equal(["2.1", "2.1.1", "2.1.2", "2.2", "2.3", "2.3.1", "2.3.2"], preview.Headings.Select(h => h.Label));
        Assert.Equal("Cezalandırılabilirliğin Diğer Şartları", preview.Headings[3].Title);
        Assert.Contains(preview.Warnings, w => w.Message.Contains("Aynı üst başlık altında aynı ad"));
        Assert.Equal(before.Count, (await CoursesAsync(admin)).Count); // önizleme kayıt yapmaz

        var result = await (await admin.PostJsonAsync("/api/admin/lecture-courses/import",
            new LectureImportRequest(text, preview.Title, preview.ChapterNumber, Visibility.Public))).ReadAsync<LectureImportResult>(201);
        Assert.Equal(7, result.HeadingCount);

        var courses = await CoursesAsync(admin);
        Assert.Equal((result.CourseId, 2, 7), (courses[^1].Id, courses[^1].ChapterNumber!.Value, courses[^1].NodeCount));
        var toc = await (await admin.GetAsync($"/api/lecture-courses/{result.Slug}")).ReadAsync<LectureCourseDetail>();
        Assert.Equal(preview.Headings.Select(h => (h.Label, h.Title)), toc.Toc.Select(e => (e.Label, e.Title)));
        Assert.All(toc.Toc, e => Assert.Equal(Visibility.Public, e.Visibility));
        Assert.Equal(Visibility.Public, toc.Visibility);
    }

    /// <summary>AC-6: aynı aktarımdaki aynı adlı başlıklar (birçok "Genel Olarak") ayrı adres alır; hepsi okunur.</summary>
    [Fact]
    public async Task Import_DuplicateTitles_GetDistinctAddresses()
    {
        var admin = await factory.CreateAdminClientAsync();
        var text = $"1. Bir {tag}\n1.1. Genel Olarak\n1.2. İki {tag}\n1.2.1. Genel Olarak\n2. Üç {tag}\n2.1. Genel Olarak\n2.2. Genel Olarak";
        var result = await (await admin.PostJsonAsync("/api/admin/lecture-courses/import",
            new LectureImportRequest(text, $"Adres dersi {tag}", null, Visibility.Public))).ReadAsync<LectureImportResult>(201);

        var toc = await (await admin.GetAsync($"/api/lecture-courses/{result.Slug}")).ReadAsync<LectureCourseDetail>();
        var general = toc.Toc.Where(e => e.Title == "Genel Olarak").ToList();
        Assert.Equal(4, general.Count);
        Assert.Equal(4, general.Select(e => e.Slug).Distinct().Count());
        foreach (var entry in general)
            Assert.Equal(entry.Id, (await (await admin.GetAsync($"/api/lecture-notes/{entry.Slug}")).ReadAsync<LectureNoteDetail>()).Id);
    }

    /// <summary>AC-5/AC-6: hatalı metin, boş ad ya da geçersiz bölüm numarası 400 döner ve hiçbir ders/başlık oluşmaz.</summary>
    [Fact]
    public async Task Import_WithErrors_Returns400_AndCreatesNothing()
    {
        var admin = await factory.CreateAdminClientAsync();
        var before = await CoursesAsync(admin);

        async Task<string> RejectAsync(LectureImportRequest request, string field)
        {
            var response = await admin.PostJsonAsync("/api/admin/lecture-courses/import", request);
            Assert.Equal(400, (int)response.StatusCode);
            var problem = await response.Content.ReadAsStringAsync();
            Assert.Contains($"\"{field}\"", problem);
            return problem;
        }

        Assert.Contains("Satır 2", await RejectAsync(new($"1. Bir {tag}\n1.1.1. Üç", "Ders", null, Visibility.Public), "text"));
        await RejectAsync(new($"1. Bir {tag}", "  ", null, Visibility.Public), "title");
        await RejectAsync(new($"1. Bir {tag}", "Ders", 0, Visibility.Public), "chapterNumber");
        await RejectAsync(new($"1. Bir {tag}", "Ders", 100, Visibility.Public), "chapterNumber");
        await RejectAsync(new("düz metin", "Ders", null, Visibility.Public), "text");
        await RejectAsync(new(string.Join('\n', Enumerable.Range(1, 301).Select(i => $"{i}. B {i}")), "Ders", null, Visibility.Public), "text");

        Assert.Equal(before.Count, (await CoursesAsync(admin)).Count);
        Assert.Equal([2], (await PreviewAsync(admin, new($"1. Bir {tag}\n1.1.1. Üç", "Ders", null, false))).Errors.Select(e => e.Line));
        Assert.Contains((await PreviewAsync(admin, new("0.1. Bir", "Ders", 0, false))).Errors,
            e => e.Line == 0 && e.Message.Contains("Bölüm numarası"));
    }

    /// <summary>
    /// AC-7 (hassas): özel kurulan dersin başlıkları giriş yapmamış okuyucuya okumada, içindekilerde ve aramada yoktur
    /// (sitede site haritası yok); üye görür. Açık kurulan ders herkese görünür. Başlıkların kendi ayarı açıktır.
    /// </summary>
    [Fact]
    public async Task Import_PrivateByDefault_HiddenFromAnonymous_VisibleToMember()
    {
        var admin = await factory.CreateAdminClientAsync();
        var anonymous = factory.CreateAnonymousClient();
        var member = await CreateUserClientAsync(admin, Roles.Standard);

        async Task<(LectureImportResult Result, string Word)> ImportAsync(Visibility visibility)
        {
            var word = Word(visibility == Visibility.Private ? "gizliaktar" : "acikaktar");
            var result = await (await admin.PostJsonAsync("/api/admin/lecture-courses/import",
                new LectureImportRequest($"1. Giriş {word}\n1.1. Alt başlık {word}", $"Görünürlük {tag} {visibility}", null, visibility)))
                .ReadAsync<LectureImportResult>(201);
            return (result, word);
        }

        async Task AssertVisibleAsync(HttpClient client, LectureImportResult course, string word, bool visible)
        {
            await client.GetAsync($"/api/lecture-courses/{course.Slug}").ExpectAsync(visible ? 200 : 404);
            var listed = (await (await client.GetAsync("/api/lecture-courses")).ReadAsync<List<LectureCourseSummary>>()).Any(c => c.Id == course.CourseId);
            Assert.Equal(visible, listed);
            Assert.Equal(visible ? 2 : 0, (await SearchAsync(client, word)).Count);
        }

        var (secret, secretWord) = await ImportAsync(Visibility.Private);
        var adminToc = await (await admin.GetAsync($"/api/lecture-courses/{secret.Slug}")).ReadAsync<LectureCourseDetail>();
        Assert.All(adminToc.Toc, e => Assert.Equal((Visibility.Public, Visibility.Private), (e.Visibility, e.EffectiveVisibility)));
        await AssertVisibleAsync(anonymous, secret, secretWord, false);
        foreach (var entry in adminToc.Toc)
            await anonymous.GetAsync($"/api/lecture-notes/{entry.Slug}").ExpectAsync(404);
        await AssertVisibleAsync(member, secret, secretWord, true);

        var (open, openWord) = await ImportAsync(Visibility.Public);
        await AssertVisibleAsync(anonymous, open, openWord, true);
    }

    /// <summary>
    /// AC-8 (hassas): önizleme, içe aktarma ve bölüm numarası yalnız yöneticiye açık — giriş yok 401, standard ve reader
    /// 403; hiçbir ders oluşmaz, numara değişmez.
    /// </summary>
    [Fact]
    public async Task Import_AndChapterNumber_AdminOnly()
    {
        var admin = await factory.CreateAdminClientAsync();
        var course = await CreateCourseAsync(admin, $"Yetki {tag}");
        var before = await CoursesAsync(admin);
        var preview = new LectureImportPreviewRequest($"1. Bir {tag}", "Ders", null, true);
        var import = new LectureImportRequest($"1. Bir {tag}", $"Yetkisiz {tag}", null, Visibility.Public);

        foreach (var (client, status) in new[]
        {
            (factory.CreateAnonymousClient(), 401),
            (await CreateUserClientAsync(admin, Roles.Standard), 403),
            (await CreateUserClientAsync(admin, Roles.Reader), 403),
        })
        {
            await client.PostJsonAsync("/api/admin/lecture-courses/import/preview", preview).ExpectAsync(status);
            await client.PostJsonAsync("/api/admin/lecture-courses/import", import).ExpectAsync(status);
            await SaveCourseAsync(client, course.Id, course.Title, 3).ExpectAsync(status);
        }

        var after = await CoursesAsync(admin);
        Assert.Equal(before.Count, after.Count);
        Assert.Null(after.Single(c => c.Id == course.Id).ChapterNumber);
    }

    private static async Task<List<LectureCourseSummary>> CoursesAsync(HttpClient client) =>
        await (await client.GetAsync("/api/lecture-courses")).ReadAsync<List<LectureCourseSummary>>();

    private static async Task<LectureImportPreview> PreviewAsync(HttpClient admin, LectureImportPreviewRequest request) =>
        await (await admin.PostJsonAsync("/api/admin/lecture-courses/import/preview", request)).ReadAsync<LectureImportPreview>();

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
