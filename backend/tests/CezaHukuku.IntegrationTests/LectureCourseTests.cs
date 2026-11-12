using System.Diagnostics;
using System.Net.Http.Headers;
using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Content;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Search;
using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Users;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Infrastructure.Search;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit.Abstractions;

namespace CezaHukuku.IntegrationTests;

/// <summary>Spec 0002: dersler, başlık ağacı, numaralar, taşıma, etkin görünürlük ve yönetim yetkisi.</summary>
[Collection(ApiCollection.Name)]
public class LectureCourseTests(ApiFactory factory, ITestOutputHelper output)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
    private readonly string tag = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Aramada tek parça bulunan, bu teste özgü sözcük (yalnız harf).</summary>
    private string Word(string prefix) => prefix + new string([.. tag.Select(ch => (char)('k' + ch % 16))]);

    [Fact]
    public async Task Courses_AreListed_AndFlatNotesKeepTheirAddresses()
    {
        var admin = await factory.CreateAdminClientAsync();
        var flat = await (await admin.PostJsonAsync("/api/admin/lecture-notes",
            new SaveLectureNoteRequest($"Ceza genel {tag}", "<p>Düz not.</p>", Visibility.Public, null, null, null))).ReadAsync<LectureNoteDetail>(201);

        var first = await CreateCourseAsync(admin, $"Birinci ders {tag}", description: "Genel hükümler.");
        var second = await CreateCourseAsync(admin, $"İkinci ders {tag}");
        // Ders ve düz not aynı /ders-notlari/{adres} alanında: aynı ad ayrı adres alır; "yeni" ayrılmış.
        var sameAsFlat = await CreateCourseAsync(admin, $"Ceza genel {tag}");
        var reserved = await CreateCourseAsync(admin, "Yeni");
        Assert.Equal(flat.Slug + "-2", sameAsFlat.Slug);
        Assert.StartsWith("yeni-ders", reserved.Slug);
        var node = await AddNodeAsync(admin, first.Id, $"Ceza genel {tag}");
        Assert.NotEqual(flat.Slug, node.Slug);

        var anonymous = factory.CreateAnonymousClient();
        var list = await (await anonymous.GetAsync("/api/lecture-courses")).ReadAsync<List<LectureCourseSummary>>();
        var mine = list.Where(c => c.Title.EndsWith(tag)).Select(c => c.Id).ToList();
        Assert.Equal([first.Id, second.Id, sameAsFlat.Id], mine);
        var firstSummary = list.Single(c => c.Id == first.Id);
        Assert.Equal(("Genel hükümler.", 1), (firstSummary.Description, firstSummary.NodeCount));

        // Sıra değişir.
        var allIds = (await (await admin.GetAsync("/api/lecture-courses")).ReadAsync<List<LectureCourseSummary>>()).Select(c => c.Id).ToList();
        allIds.Remove(second.Id);
        allIds.Insert(0, second.Id);
        await admin.PutJsonAsync("/api/admin/lecture-courses/order", new ReorderLectureCoursesRequest(allIds)).ExpectAsync(204);
        list = await (await anonymous.GetAsync("/api/lecture-courses")).ReadAsync<List<LectureCourseSummary>>();
        Assert.Equal([second.Id, first.Id, sameAsFlat.Id], list.Where(c => c.Title.EndsWith(tag)).Select(c => c.Id));
        await admin.PutJsonAsync("/api/admin/lecture-courses/order", new ReorderLectureCoursesRequest([second.Id])).ExpectAsync(400);

        // Düz notlar eski adresleriyle açılır ve düz not listesinde kalır; başlıklar o listede yok.
        var flatDetail = await (await anonymous.GetAsync($"/api/lecture-notes/{flat.Slug}")).ReadAsync<LectureNoteDetail>();
        Assert.Equal(("<p>Düz not.</p>", null), (flatDetail.Html, flatDetail.Node));
        var flatList = await (await anonymous.GetAsync("/api/lecture-notes")).ReadAsync<List<LectureNoteSummary>>();
        Assert.Contains(flatList, n => n.Id == flat.Id);
        Assert.DoesNotContain(flatList, n => n.Id == node.Id);

        // Ders adı ve açıklaması değişir, adresi değişmez.
        var renamed = await (await admin.PutJsonAsync($"/api/admin/lecture-courses/{first.Id}",
            new SaveLectureCourseRequest($"Yeni ad {tag}", "  ", Visibility.Public))).ReadAsync<LectureCourseDetail>();
        Assert.Equal((first.Slug, $"Yeni ad {tag}", ""), (renamed.Slug, renamed.Title, renamed.Description));
        await admin.PostJsonAsync("/api/admin/lecture-courses", new SaveLectureCourseRequest(" ", null, Visibility.Public)).ExpectAsync(400);
    }

    [Fact]
    public async Task Nodes_AreAddedAtRootAndUnderParent_SameTitlesGetDistinctAddresses()
    {
        var admin = await factory.CreateAdminClientAsync();
        var course = await CreateCourseAsync(admin, $"Ağaç dersi {tag}");

        var genel = await AddNodeAsync(admin, course.Id, $"Genel hükümler {tag}");
        await AddNodeAsync(admin, course.Id, $"Özel hükümler {tag}");
        LectureNoteDetail level2 = null!;
        for (var i = 1; i <= 5; i++)
            level2 = await AddNodeAsync(admin, course.Id, $"İkinci düzey {i} {tag}", genel.Id);
        var level3 = await AddNodeAsync(admin, course.Id, $"Yer bakımından {tag}", level2.Id);
        var level4 = await AddNodeAsync(admin, course.Id, $"Zaman bakımından {tag}", level3.Id);
        var a = await AddNodeAsync(admin, course.Id, $"Genel olarak {tag}", level4.Id);
        await AddNodeAsync(admin, course.Id, $"Beşinci düzey b {tag}", level4.Id);
        var c = await AddNodeAsync(admin, course.Id, $"Beşinci düzey c {tag}", level4.Id);
        var level6 = await AddNodeAsync(admin, course.Id, $"Genel olarak {tag}", c.Id);
        var level6b = await AddNodeAsync(admin, course.Id, $"Genel olarak {tag}", c.Id);
        var other = await AddNodeAsync(admin, course.Id, $"Genel olarak {tag}", level3.Id);

        // Numaralar sıradan: birinci düzey "1. Bölüm", altıncı düzeyde "1.5.1.1.3.1".
        Assert.Equal(("1", "1. Bölüm", 1), (genel.Node!.Number, genel.Node.Label, genel.Node.Depth));
        Assert.Equal(("1.5.1.1.3.1", 6), (level6.Node!.Label, level6.Node.Depth));
        Assert.Equal("1.5.1.1.3.2", level6b.Node!.Label);
        Assert.Equal("1.5.1.2", other.Node!.Label);
        Assert.Equal([$"Genel hükümler {tag}", $"İkinci düzey 5 {tag}", $"Yer bakımından {tag}", $"Zaman bakımından {tag}", $"Beşinci düzey c {tag}"],
            level6.Node.Path.Select(p => p.Title));
        Assert.Equal(course.Slug, level6.Node.Course.Slug);

        // Aynı adlı başlıklar ayrı adres: önce ad, sonra üst başlığın adıyla, sonra "-2".
        Assert.Equal(4, new[] { a.Slug, level6.Slug, level6b.Slug, other.Slug }.Distinct().Count());
        Assert.Equal($"{c.Slug}-{a.Slug}", level6.Slug);
        Assert.Equal($"{c.Slug}-{a.Slug}-2", level6b.Slug);
        Assert.Equal($"{level3.Slug}-{a.Slug}", other.Slug);

        var anonymous = factory.CreateAnonymousClient();
        var toc = await TocAsync(anonymous, course.Slug);
        Assert.Equal(["1. Bölüm", "1.1", "1.2", "1.3", "1.4", "1.5", "1.5.1", "1.5.1.1", "1.5.1.1.1", "1.5.1.1.2", "1.5.1.1.3",
            "1.5.1.1.3.1", "1.5.1.1.3.2", "1.5.1.2", "2. Bölüm"], toc.Toc.Select(e => e.Label));
        Assert.Equal($"Özel hükümler {tag}", toc.Toc[^1].Title);
        Assert.Equal(level4.Id, toc.Toc.Single(e => e.Id == c.Id).ParentId);
        foreach (var node in new[] { a, level6, level6b, other })
            Assert.Equal(node.Title, (await (await anonymous.GetAsync($"/api/lecture-notes/{node.Slug}")).ReadAsync<LectureNoteDetail>()).Title);

        // Geçersiz: boş ad, başka dersin başlığı üst başlık olarak, olmayan ders.
        var otherCourse = await CreateCourseAsync(admin, $"Başka ders {tag}");
        await admin.PostJsonAsync($"/api/admin/lecture-courses/{course.Id}/nodes", new AddLectureNodeRequest(" ", null, Visibility.Public)).ExpectAsync(400);
        await admin.PostJsonAsync($"/api/admin/lecture-courses/{otherCourse.Id}/nodes", new AddLectureNodeRequest("X", genel.Id, Visibility.Public)).ExpectAsync(400);
        await admin.PostJsonAsync($"/api/admin/lecture-courses/{Guid.NewGuid()}/nodes", new AddLectureNodeRequest("X", null, Visibility.Public)).ExpectAsync(404);
        Assert.Empty((await TocAsync(admin, otherCourse.Slug)).Toc);
    }

    [Fact]
    public async Task Move_RenumbersSubtree_KeepsAddresses_RejectsMoveUnderOwnDescendant()
    {
        var admin = await factory.CreateAdminClientAsync();
        var course = await CreateCourseAsync(admin, $"Taşıma dersi {tag}");
        var a = await AddNodeAsync(admin, course.Id, $"A {tag}");
        var a1 = await AddNodeAsync(admin, course.Id, $"A1 {tag}", a.Id);
        var a2 = await AddNodeAsync(admin, course.Id, $"A2 {tag}", a.Id);
        var a21 = await AddNodeAsync(admin, course.Id, $"A21 {tag}", a2.Id);
        var b = await AddNodeAsync(admin, course.Id, $"B {tag}");
        var slugs = (await TocAsync(admin, course.Slug)).Toc.ToDictionary(e => e.Id, e => e.Slug);

        // A2'yi (altıyla) B'nin altına.
        var moved = await (await MoveAsync(admin, a2.Id, b.Id, 0)).ReadAsync<LectureNoteDetail>();
        Assert.Equal(("2.1", b.Id), (moved.Node!.Label, moved.Node.ParentId));
        var toc = await TocAsync(admin, course.Slug);
        Assert.Equal([("1. Bölüm", a.Id), ("1.1", a1.Id), ("2. Bölüm", b.Id), ("2.1", a2.Id), ("2.1.1", a21.Id)],
            toc.Toc.Select(e => (e.Label, e.Id)));

        // Aynı düzeyde yukarı: B, A'nın önüne.
        await MoveAsync(admin, b.Id, null, 0).ExpectAsync(200);
        toc = await TocAsync(admin, course.Slug);
        Assert.Equal([("1. Bölüm", b.Id), ("1.1", a2.Id), ("1.1.1", a21.Id), ("2. Bölüm", a.Id), ("2.1", a1.Id)],
            toc.Toc.Select(e => (e.Label, e.Id)));
        // Sıra kardeş sayısını aşarsa sona.
        await MoveAsync(admin, b.Id, null, 99).ExpectAsync(200);
        Assert.Equal([a.Id, a1.Id, b.Id, a2.Id, a21.Id], (await TocAsync(admin, course.Slug)).Toc.Select(e => e.Id));

        // Adresler değişmedi.
        Assert.All((await TocAsync(admin, course.Slug)).Toc, e => Assert.Equal(slugs[e.Id], e.Slug));

        // Kendi altına, kendi alt başlığının altına, başka derse, negatif sıra ve düz not: reddedilir, ağaç değişmez.
        var before = (await TocAsync(admin, course.Slug)).Toc;
        await MoveAsync(admin, b.Id, a21.Id, 0).ExpectAsync(400);
        await MoveAsync(admin, b.Id, b.Id, 0).ExpectAsync(400);
        var otherCourse = await CreateCourseAsync(admin, $"Öteki ders {tag}");
        var foreign = await AddNodeAsync(admin, otherCourse.Id, $"Yabancı {tag}");
        await MoveAsync(admin, b.Id, foreign.Id, 0).ExpectAsync(400);
        await MoveAsync(admin, b.Id, null, -1).ExpectAsync(400);
        var flat = await (await admin.PostJsonAsync("/api/admin/lecture-notes",
            new SaveLectureNoteRequest($"Düz {tag}", "", Visibility.Public, null, null, null))).ReadAsync<LectureNoteDetail>(201);
        await MoveAsync(admin, flat.Id, null, 0).ExpectAsync(400);
        await MoveAsync(admin, Guid.NewGuid(), null, 0).ExpectAsync(404);
        Assert.Equal(before, (await TocAsync(admin, course.Slug)).Toc);
    }

    [Fact]
    public async Task Node_HoldsTextFilesAndArticleLink()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (_, law, article) = await CreateLawWithArticleAsync(admin);
        var course = await CreateCourseAsync(admin, $"İçerik dersi {tag}");
        var parent = await AddNodeAsync(admin, course.Id, $"Üst {tag}");
        var node = await AddNodeAsync(admin, course.Id, $"İçerikli {tag}", parent.Id);
        var child = await AddNodeAsync(admin, course.Id, $"Alt {tag}", node.Id);
        node = await (await admin.GetAsync($"/api/lecture-notes/{node.Slug}")).ReadAsync<LectureNoteDetail>();

        // Yeni başlık içeriksizdir; alt başlıkları ve önceki/sonraki ile gelir.
        Assert.Equal(("", 0), (node.Html, node.Files.Count));
        Assert.Equal([child.Id], node.Node!.Children.Select(ch => ch.Id));
        Assert.Equal((parent.Id, child.Id), (node.Node.Previous?.Id, node.Node.Next?.Id));

        var updated = await (await admin.PutJsonAsync($"/api/admin/lecture-notes/{node.Id}",
            new SaveLectureNoteRequest($"İçerikli (düzeltildi) {tag}", "<p>Anlatım.</p><script>x</script>", Visibility.Public, null, null, article.Id)))
            .ReadAsync<LectureNoteDetail>();
        Assert.Equal(("<p>Anlatım.</p>", node.Slug, "1.1"), (updated.Html, updated.Slug, updated.Node!.Label));
        Assert.Equal((law.Id, article.Id), (updated.LawId, updated.ArticleId));

        var file = await (await admin.PostAsync($"/api/admin/lecture-notes/{node.Id}/files", Form(Png, "sema.png"))).ReadAsync<LectureNoteFileDto>(201);

        var anonymous = factory.CreateAnonymousClient();
        var detail = await (await anonymous.GetAsync($"/api/lecture-notes/{node.Slug}")).ReadAsync<LectureNoteDetail>();
        Assert.Equal(("<p>Anlatım.</p>", $"İçerikli (düzeltildi) {tag}"), (detail.Html, detail.Title));
        Assert.Equal([file.Id], detail.Files.Select(f => f.Id));
        Assert.Equal("81", detail.Scope.Article?.Number);
        Assert.Equal(course.Id, detail.Node!.Course.Id);
        await anonymous.GetAsync(file.Url).ExpectAsync(200);
        // Düzenleme başlığı ağaçtaki yerinden oynatmaz.
        Assert.Equal(parent.Id, detail.Node.ParentId);
    }

    [Fact]
    public async Task PrivateCourseOrParent_HidesSubtreeFromAnonymous_InTocDetailFilesAndSearch()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (_, law, article) = await CreateLawWithArticleAsync(admin);
        var vis = Word("gorunur");

        // Açık derste özel üst başlık: kendi ayarı açık olan alt başlık da özel sayılır.
        var course = await CreateCourseAsync(admin, $"Görünürlük dersi {tag}");
        var a = await AddNodeAsync(admin, course.Id, $"Açık A {vis}");
        var secret = await AddNodeAsync(admin, course.Id, $"Özel üst {vis}", visibility: Visibility.Private);
        var inner = await AddNodeAsync(admin, course.Id, $"Açık iç {vis}", secret.Id);
        var c = await AddNodeAsync(admin, course.Id, $"Açık C {vis}");
        await LinkToArticleAsync(admin, inner, article.Id);
        var innerFile = await (await admin.PostAsync($"/api/admin/lecture-notes/{inner.Id}/files", Form(Png, "ic.png"))).ReadAsync<LectureNoteFileDto>(201);

        // Özel ders: başlığın kendi ayarı açık.
        var privateCourse = await CreateCourseAsync(admin, $"Özel ders {tag}", Visibility.Private);
        var underPrivate = await AddNodeAsync(admin, privateCourse.Id, $"Özel dersin başlığı {vis}");
        await LinkToArticleAsync(admin, underPrivate, article.Id);
        var underPrivateFile = await (await admin.PostAsync($"/api/admin/lecture-notes/{underPrivate.Id}/files", Form(Png, "o.png"))).ReadAsync<LectureNoteFileDto>(201);

        var anonymous = factory.CreateAnonymousClient();
        var toc = await TocAsync(anonymous, course.Slug);
        Assert.Equal([a.Id, c.Id], toc.Toc.Select(e => e.Id));
        // Gizli başlık numarayı kaydırmaz: herkes için aynı numara aynı başlıktır.
        Assert.Equal(["1. Bölüm", "3. Bölüm"], toc.Toc.Select(e => e.Label));
        await anonymous.GetAsync($"/api/lecture-notes/{secret.Slug}").ExpectAsync(404);
        await anonymous.GetAsync($"/api/lecture-notes/{inner.Slug}").ExpectAsync(404);
        await anonymous.GetAsync(innerFile.Url).ExpectAsync(404);
        var aDetail = await (await anonymous.GetAsync($"/api/lecture-notes/{a.Slug}")).ReadAsync<LectureNoteDetail>();
        Assert.Equal(c.Id, aDetail.Node!.Next?.Id);

        await anonymous.GetAsync($"/api/lecture-courses/{privateCourse.Slug}").ExpectAsync(404);
        Assert.DoesNotContain(await (await anonymous.GetAsync("/api/lecture-courses")).ReadAsync<List<LectureCourseSummary>>(), x => x.Id == privateCourse.Id);
        await anonymous.GetAsync($"/api/lecture-notes/{underPrivate.Slug}").ExpectAsync(404);
        await anonymous.GetAsync(underPrivateFile.Url).ExpectAsync(404);

        var articleDetail = await (await anonymous.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81")).ReadAsync<ArticleDetailResponse>();
        Assert.Empty(articleDetail.LectureNotes);

        // Aramada yalnız açık başlıklar (başlık adında ve belge adında geçen sözcükle).
        var anonymousHits = await SearchAsync(anonymous, vis);
        Assert.Equal([$"/ders-notlari/{course.Slug}/{a.Slug}", $"/ders-notlari/{course.Slug}/{c.Slug}"], anonymousHits.Select(h => h.Url).Order());
        var adminHits = await SearchAsync(admin, vis);
        Assert.Contains(adminHits, h => h.Url == $"/ders-notlari/{course.Slug}/{inner.Slug}");
        Assert.Contains(adminHits, h => h.Url == $"/ders-notlari/{privateCourse.Slug}/{underPrivate.Slug}");
        Assert.Contains(adminHits, h => h.Url.StartsWith($"/ders-notlari/{course.Slug}/{inner.Slug}#belge-"));

        // Giriş yapmış üye görür; etkin görünürlük "özel" (rozet için).
        foreach (var role in new[] { Roles.Standard, Roles.Reader })
        {
            var member = await CreateUserClientAsync(admin, role);
            var memberToc = await TocAsync(member, course.Slug);
            Assert.Equal([a.Id, secret.Id, inner.Id, c.Id], memberToc.Toc.Select(e => e.Id));
            var innerEntry = memberToc.Toc.Single(e => e.Id == inner.Id);
            Assert.Equal((Visibility.Public, Visibility.Private), (innerEntry.Visibility, innerEntry.EffectiveVisibility));
            var innerDetail = await (await member.GetAsync($"/api/lecture-notes/{inner.Slug}")).ReadAsync<LectureNoteDetail>();
            Assert.Equal(Visibility.Private, innerDetail.Node!.EffectiveVisibility);
            await member.GetAsync(innerFile.Url).ExpectAsync(200);
            var privateToc = await TocAsync(member, privateCourse.Slug);
            Assert.Equal(Visibility.Private, privateToc.Toc.Single().EffectiveVisibility);
            await member.GetAsync(underPrivateFile.Url).ExpectAsync(200);
            var memberArticle = await (await member.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81")).ReadAsync<ArticleDetailResponse>();
            Assert.Equal(2, memberArticle.LectureNotes.Count);
        }

        // Dersi açınca başlıkları anonime açılır; üst başlığı açınca alt başlık da.
        await admin.PutJsonAsync($"/api/admin/lecture-courses/{privateCourse.Id}",
            new SaveLectureCourseRequest(privateCourse.Title, null, Visibility.Public)).ExpectAsync(200);
        await anonymous.GetAsync($"/api/lecture-notes/{underPrivate.Slug}").ExpectAsync(200);
        await admin.PutJsonAsync($"/api/admin/lecture-notes/{secret.Id}",
            new SaveLectureNoteRequest(secret.Title, "", Visibility.Public, null, null, null)).ExpectAsync(200);
        await anonymous.GetAsync($"/api/lecture-notes/{inner.Slug}").ExpectAsync(200);
        // Arama indeksi de ders/üst başlık değişikliğiyle alt başlıkları yeniden yazar.
        anonymousHits = await SearchAsync(anonymous, vis);
        Assert.Contains(anonymousHits, h => h.Url == $"/ders-notlari/{course.Slug}/{inner.Slug}");
        Assert.Contains(anonymousHits, h => h.Url == $"/ders-notlari/{privateCourse.Slug}/{underPrivate.Slug}");
    }

    [Fact]
    public async Task Search_FindsNodeTitleTextAndFile_WithNumberAndPath()
    {
        var admin = await factory.CreateAdminClientAsync();
        var course = await CreateCourseAsync(admin, $"Arama dersi {tag}");
        var root = await AddNodeAsync(admin, course.Id, $"Genel hükümler {tag}");
        await AddNodeAsync(admin, course.Id, $"Önce gelen {tag}", root.Id);
        var (titleWord, textWord, fileWord) = (Word("baslik"), Word("metin"), Word("belge"));
        var node = await AddNodeAsync(admin, course.Id, $"Kast {titleWord}", root.Id);
        await admin.PutJsonAsync($"/api/admin/lecture-notes/{node.Id}",
            new SaveLectureNoteRequest(node.Title, $"<p>Olası kast {textWord} anlatımı.</p>", Visibility.Public, null, null, null)).ExpectAsync(200);
        var file = await (await admin.PostAsync($"/api/admin/lecture-notes/{node.Id}/files", Form(BuildPdf($"Sunum {fileWord}"), "sunum.pdf")))
            .ReadAsync<LectureNoteFileDto>(201);

        var anonymous = factory.CreateAnonymousClient();
        var url = $"/ders-notlari/{course.Slug}/{node.Slug}";
        var context = $"Arama dersi {tag} › Genel hükümler {tag}";
        var byTitle = Assert.Single(await SearchAsync(anonymous, titleWord), h => !h.Url.Contains('#'));
        Assert.Equal(($"1.2 Kast {titleWord}", context, url), (byTitle.Title, byTitle.Context, byTitle.Url));
        var byText = Assert.Single(await SearchAsync(anonymous, textWord));
        Assert.Equal(url, byText.Url);
        var byFile = Assert.Single(await SearchAsync(anonymous, fileWord));
        Assert.Equal(($"{url}#belge-{file.Id}", $"{context} · ekli belge"), (byFile.Url, byFile.Context));

        // Taşınınca numara ve ders yolu güncellenir; silinince aramada çıkmaz.
        await MoveAsync(admin, node.Id, null, 0).ExpectAsync(200);
        var moved = Assert.Single(await SearchAsync(anonymous, titleWord), h => !h.Url.Contains('#'));
        Assert.Equal(($"1. Bölüm Kast {titleWord}", $"Arama dersi {tag}", url), (moved.Title, moved.Context, moved.Url));
        Assert.Equal("2. Bölüm", (await TocAsync(anonymous, course.Slug)).Toc.Single(e => e.Id == root.Id).Label);
        await admin.DeleteAsync($"/api/admin/lecture-notes/{node.Id}").ExpectAsync(204);
        Assert.Empty(await SearchAsync(anonymous, titleWord));
        Assert.Empty(await SearchAsync(anonymous, textWord));
        Assert.Empty(await SearchAsync(anonymous, fileWord));
    }

    /// <summary>Plan riski: dersteki her değişiklikte dersin tamamı yeniden indekslenir. 300 başlıkta süre ölçülür.</summary>
    [Fact]
    public async Task Search_ReindexOfThreeHundredNodeCourse_IsMeasured()
    {
        var admin = await factory.CreateAdminClientAsync();
        var course = await CreateCourseAsync(admin, $"Büyük ders {tag}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var parents = new List<LectureNote>();
            for (var i = 0; i < 300; i++)
            {
                var parent = i < 30 ? null : parents[i % 30];
                var note = new LectureNote
                {
                    Title = $"Başlık {i} {tag}", Slug = $"b{i}-{tag}", CourseId = course.Id, ParentId = parent?.Id, SortOrder = i,
                    Html = "<p>Kısa bir anlatım metni, birkaç cümle.</p>", PlainText = "Kısa bir anlatım metni, birkaç cümle.",
                };
                parents.Add(note);
                db.LectureNotes.Add(note);
            }
            await db.SaveChangesAsync();
        }
        var toc = await TocAsync(admin, course.Slug);
        Assert.Equal(300, toc.Toc.Count);
        var target = toc.Toc[^1];

        var changed = Word("degisti");
        var watch = Stopwatch.StartNew();
        await admin.PutJsonAsync($"/api/admin/lecture-notes/{target.Id}",
            new SaveLectureNoteRequest(target.Title, $"<p>{changed}</p>", Visibility.Public, null, null, null)).ExpectAsync(200);
        watch.Stop();
        output.WriteLine($"300 başlıklı derste bir başlığın kaydı (dersin yeniden indekslenmesiyle): {watch.ElapsedMilliseconds} ms");

        var hit = Assert.Single(await SearchAsync(admin, changed));
        Assert.Equal($"{target.Label} {target.Title}", hit.Title);
    }

    [Fact]
    public async Task NonAdmins_CannotManageCoursesOrNodes()
    {
        var admin = await factory.CreateAdminClientAsync();
        var course = await CreateCourseAsync(admin, $"Yetki dersi {tag}");
        var node = await AddNodeAsync(admin, course.Id, $"Yetki başlığı {tag}");
        var leaf = await AddNodeAsync(admin, course.Id, $"Yaprak {tag}", node.Id);
        var before = await TocAsync(admin, course.Slug);

        var clients = new (HttpClient Client, int Status)[]
        {
            (factory.CreateAnonymousClient(), 401),
            (await CreateUserClientAsync(admin, Roles.Standard), 403),
            (await CreateUserClientAsync(admin, Roles.Reader), 403),
        };
        foreach (var (client, status) in clients)
        {
            await client.PostJsonAsync("/api/admin/lecture-courses", new SaveLectureCourseRequest("X", null, Visibility.Public)).ExpectAsync(status);
            await client.PutJsonAsync($"/api/admin/lecture-courses/{course.Id}", new SaveLectureCourseRequest("X", null, Visibility.Private)).ExpectAsync(status);
            await client.PutJsonAsync("/api/admin/lecture-courses/order", new ReorderLectureCoursesRequest([course.Id])).ExpectAsync(status);
            await client.DeleteAsync($"/api/admin/lecture-courses/{course.Id}").ExpectAsync(status);
            await client.PostJsonAsync($"/api/admin/lecture-courses/{course.Id}/nodes", new AddLectureNodeRequest("X", null, Visibility.Public)).ExpectAsync(status);
            await client.PutJsonAsync($"/api/admin/lecture-notes/{leaf.Id}/position", new MoveLectureNodeRequest(null, 0)).ExpectAsync(status);
            await client.PutJsonAsync($"/api/admin/lecture-notes/{leaf.Id}", new SaveLectureNoteRequest("X", "", Visibility.Private, null, null, null)).ExpectAsync(status);
            await client.DeleteAsync($"/api/admin/lecture-notes/{leaf.Id}").ExpectAsync(status);
        }

        var after = await TocAsync(admin, course.Slug);
        Assert.Equal((before.Title, before.Visibility), (after.Title, after.Visibility));
        Assert.Equal(before.Toc, after.Toc);
        Assert.Equal($"Yaprak {tag}", (await (await admin.GetAsync($"/api/lecture-notes/{leaf.Slug}")).ReadAsync<LectureNoteDetail>()).Title);
    }

    [Fact]
    public async Task Delete_OnlyLeafNodesAndEmptyCourses()
    {
        var admin = await factory.CreateAdminClientAsync();
        var course = await CreateCourseAsync(admin, $"Silme dersi {tag}");
        var parent = await AddNodeAsync(admin, course.Id, $"Üst başlık {tag}");
        var leaf = await AddNodeAsync(admin, course.Id, $"Yaprak başlık {tag}", parent.Id);
        var file = await (await admin.PostAsync($"/api/admin/lecture-notes/{leaf.Id}/files", Form(Png, "y.png"))).ReadAsync<LectureNoteFileDto>(201);

        // Alt başlığı olan başlık ve başlığı olan ders silinmez; gerekçeli mesaj.
        var problem = await (await admin.DeleteAsync($"/api/admin/lecture-notes/{parent.Id}")).Content.ReadAsStringAsync();
        Assert.Contains("Alt başlığı olan başlık silinemez", problem);
        await admin.DeleteAsync($"/api/admin/lecture-notes/{parent.Id}").ExpectAsync(400);
        await admin.DeleteAsync($"/api/admin/lecture-courses/{course.Id}").ExpectAsync(400);
        Assert.Equal(2, (await TocAsync(admin, course.Slug)).Toc.Count);

        // Yaprak silinir: belgesiyle birlikte okuyucudan kalkar.
        await admin.DeleteAsync($"/api/admin/lecture-notes/{leaf.Id}").ExpectAsync(204);
        Assert.Equal([parent.Id], (await TocAsync(admin, course.Slug)).Toc.Select(e => e.Id));
        await admin.GetAsync($"/api/lecture-notes/{leaf.Slug}").ExpectAsync(404);
        await admin.GetAsync(file.Url).ExpectAsync(404);
        Assert.Empty((await (await admin.GetAsync($"/api/lecture-notes/{parent.Slug}")).ReadAsync<LectureNoteDetail>()).Node!.Children);

        // Sonra üst başlık, sonra ders.
        await admin.DeleteAsync($"/api/admin/lecture-notes/{parent.Id}").ExpectAsync(204);
        await admin.DeleteAsync($"/api/admin/lecture-courses/{course.Id}").ExpectAsync(204);
        await admin.GetAsync($"/api/lecture-courses/{course.Slug}").ExpectAsync(404);
        Assert.DoesNotContain(await (await admin.GetAsync("/api/lecture-courses")).ReadAsync<List<LectureCourseSummary>>(), c => c.Id == course.Id);
        await admin.DeleteAsync($"/api/admin/lecture-courses/{course.Id}").ExpectAsync(404);
    }

    [Fact]
    public async Task ArticleLinkedNode_ShowsNumberAndAddressOnArticle()
    {
        var admin = await factory.CreateAdminClientAsync();
        var (_, law, article) = await CreateLawWithArticleAsync(admin);
        var course = await CreateCourseAsync(admin, $"Madde dersi {tag}");
        var root = await AddNodeAsync(admin, course.Id, $"Kök {tag}");
        await AddNodeAsync(admin, course.Id, $"Birinci {tag}", root.Id);
        var node = await AddNodeAsync(admin, course.Id, $"Kasten öldürme {tag}", root.Id);
        await LinkToArticleAsync(admin, node, article.Id);
        var flat = await (await admin.PostJsonAsync("/api/admin/lecture-notes",
            new SaveLectureNoteRequest($"Düz madde notu {tag}", "", Visibility.Public, null, null, article.Id))).ReadAsync<LectureNoteDetail>(201);

        var anonymous = factory.CreateAnonymousClient();
        var detail = await (await anonymous.GetAsync($"/api/library/laws/{law.Slug}/articles/madde-81")).ReadAsync<ArticleDetailResponse>();
        Assert.Equal(
            [
                new LectureNoteRef(flat.Slug, flat.Title, $"/ders-notlari/{flat.Slug}", null, null),
                new LectureNoteRef(node.Slug, node.Title, $"/ders-notlari/{course.Slug}/{node.Slug}", "1.2", course.Title),
            ],
            detail.LectureNotes);
    }

    private static async Task<LectureCourseDetail> CreateCourseAsync(
        HttpClient admin, string title, Visibility visibility = Visibility.Public, string? description = null) =>
        await (await admin.PostJsonAsync("/api/admin/lecture-courses", new SaveLectureCourseRequest(title, description, visibility)))
            .ReadAsync<LectureCourseDetail>(201);

    private static async Task<LectureNoteDetail> AddNodeAsync(
        HttpClient admin, Guid courseId, string title, Guid? parentId = null, Visibility visibility = Visibility.Public) =>
        await (await admin.PostJsonAsync($"/api/admin/lecture-courses/{courseId}/nodes", new AddLectureNodeRequest(title, parentId, visibility)))
            .ReadAsync<LectureNoteDetail>(201);

    private static Task<HttpResponseMessage> MoveAsync(HttpClient admin, Guid nodeId, Guid? parentId, int index) =>
        admin.PutJsonAsync($"/api/admin/lecture-notes/{nodeId}/position", new MoveLectureNodeRequest(parentId, index));

    private static async Task<LectureCourseDetail> TocAsync(HttpClient client, string slug) =>
        await (await client.GetAsync($"/api/lecture-courses/{slug}")).ReadAsync<LectureCourseDetail>();

    private static async Task LinkToArticleAsync(HttpClient admin, LectureNoteDetail node, Guid articleId) =>
        await admin.PutJsonAsync($"/api/admin/lecture-notes/{node.Id}",
            new SaveLectureNoteRequest(node.Title, node.Html, node.Visibility, null, null, articleId)).ExpectAsync(200);

    private static async Task<List<SearchHit>> SearchAsync(HttpClient client, string query) =>
        (await (await client.GetAsync($"/api/search?q={Uri.EscapeDataString(query)}&kinds=dersNotu&limit=50")).ReadAsync<SearchResponse>()).Hits;

    private static byte[] BuildPdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText(text, 11, new UglyToad.PdfPig.Core.PdfPoint(40, 750), font);
        return builder.Build();
    }

    private static MultipartFormDataContent Form(byte[] bytes, string fileName)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        form.Add(file, "file", fileName);
        return form;
    }

    private static async Task<(LawGroupSummary Group, LawSummary Law, ArticleSummary Article)> CreateLawWithArticleAsync(HttpClient admin)
    {
        var group = await (await admin.PostJsonAsync("/api/admin/law-groups", new SaveLawGroupRequest("Temel Ceza Kanunları", Slug(), null)))
            .ReadAsync<LawGroupSummary>(201);
        var law = await (await admin.PostJsonAsync("/api/admin/laws",
                new SaveLawRequest(group.Id, "5237", "Türk Ceza Kanunu", "TCK", Slug(), null, null, null, Visibility.Public, null)))
            .ReadAsync<LawSummary>(201);
        var article = await (await admin.PostJsonAsync($"/api/admin/laws/{law.Id}/articles",
                new SaveArticleRequest(null, "81", "Kasten öldürme", Visibility.Public, null)))
            .ReadAsync<ArticleSummary>(201);
        return (group, law, article);
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
