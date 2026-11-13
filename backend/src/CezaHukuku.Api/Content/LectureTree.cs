using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Text;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Content;

/// <summary>
/// Ders ağaçlarını yükler, ders notunun okuyana görünüp görünmediğine karar verir ve ders notu/ders adresleri üretir.
/// Görünürlük kararı tek yerdedir: düz ders notunda notun kendi ayarı, başlıkta dersin ağacındaki etkin görünürlük
/// (<see cref="LectureOutline"/>; özel ders ya da özel üst başlığın altı anonim okuyucuya yoktur).
/// </summary>
internal static class LectureTree
{
    private const int MaxSlugLength = 100;

    /// <summary>
    /// "/ders-notlari/yeni" yeni not formu, ".../{adres}/duzenle" düzenleme sayfasıdır; bu adresler ders, not ya da
    /// başlığa verilmez.
    /// </summary>
    private static readonly HashSet<string> Reserved = ["yeni", "duzenle"];

    /// <summary>
    /// Derslerin ağaçları. Başlıkların yalnız ağaç için gereken alanları yüklenir (metin yok); ders ≤ 300 başlıktır.
    /// </summary>
    public static async Task<Dictionary<Guid, LectureOutline>> OutlinesAsync(
        AppDbContext db, IEnumerable<Guid> courseIds, bool includePrivate, CancellationToken ct)
    {
        var ids = courseIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];
        var courses = await db.LectureCourses.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        var nodes = await NodesAsync(db, ids, ct);
        var byCourse = nodes.ToLookup(n => n.CourseId!.Value);
        return courses.ToDictionary(c => c.Id, c => LectureOutline.Build(c, byCourse[c.Id], includePrivate));
    }

    public static async Task<LectureOutline?> OutlineAsync(AppDbContext db, Guid courseId, bool includePrivate, CancellationToken ct) =>
        (await OutlinesAsync(db, [courseId], includePrivate, ct)).GetValueOrDefault(courseId);

    /// <summary>Derslerin silinmemiş başlıkları, ağaç alanlarıyla (metin ve HTML yüklenmez).</summary>
    public static Task<List<LectureNote>> NodesAsync(AppDbContext db, IReadOnlyCollection<Guid> courseIds, CancellationToken ct) =>
        db.LectureNotes.AsNoTracking()
            .Where(n => n.CourseId != null && courseIds.Contains(n.CourseId.Value))
            .Select(n => new LectureNote
            {
                Id = n.Id,
                Title = n.Title,
                Slug = n.Slug,
                Visibility = n.Visibility,
                CourseId = n.CourseId,
                ParentId = n.ParentId,
                SortOrder = n.SortOrder,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                ArticleId = n.ArticleId,
            })
            .ToListAsync(ct);

    /// <summary>
    /// Ders notu okuyana görünür mü: düz notta kendi ayarı; başlıkta dersin (okuyana göre kurulmuş) ağacında yer
    /// alması. Dersi bulunamayan başlık kimseye görünmez.
    /// </summary>
    public static bool IsVisible(LectureNote note, IReadOnlyDictionary<Guid, LectureOutline> outlines, bool includePrivate) =>
        note.CourseId is { } courseId
            ? outlines.TryGetValue(courseId, out var outline) && outline.Find(note.Id) is not null
            : includePrivate || note.Visibility == Visibility.Public;

    public static async Task<bool> IsVisibleAsync(AppDbContext db, LectureNote note, bool includePrivate, CancellationToken ct) =>
        note.CourseId is { } courseId
            ? IsVisible(note, await OutlinesAsync(db, [courseId], includePrivate, ct), includePrivate)
            : IsVisible(note, new Dictionary<Guid, LectureOutline>(), includePrivate);

    /// <summary>Başlığın okuma sayfası: /ders-notlari/{ders}/{başlık}; düz notta /ders-notlari/{not}.</summary>
    public static string UrlOf(LectureNote note, LectureCourse? course) =>
        course is null ? $"/ders-notlari/{note.Slug}" : $"/ders-notlari/{course.Slug}/{note.Slug}";

    /// <summary>
    /// Düz ders notu ya da ders için adres: "Kast ve taksir" → "kast-ve-taksir"; alınmışsa "-2", "-3"… Ders ve ders
    /// notları aynı /ders-notlari/{adres} alanını paylaştığından iki tabloya birden bakılır.
    /// </summary>
    /// <param name="batch">Toplu üretimde (içe aktarma) bu işlemde verilmiş, henüz kaydedilmemiş adresler; sonuç da eklenir.</param>
    public static async Task<string> UniqueSlugAsync(
        AppDbContext db, string title, string fallback, CancellationToken ct, ISet<string>? batch = null)
    {
        var slug = Truncate(Slugs.FromText(title));
        if (slug.Length == 0)
            slug = fallback;
        else if (Reserved.Contains(slug))
            slug = $"{slug}-{fallback}";
        return Remember(await NumberedAsync(db, slug, ct, batch), batch);
    }

    /// <summary>
    /// Başlık adresi: önce adın kendisi; alınmışsa (ör. bir derste birçok "Genel Olarak") üst başlığın adıyla
    /// ("yer-bakimindan-uygulama-genel-olarak"); o da alınmışsa "-2", "-3"…
    /// </summary>
    /// <param name="batch">Toplu üretimde (içe aktarma) bu işlemde verilmiş, henüz kaydedilmemiş adresler; sonuç da eklenir.</param>
    public static async Task<string> NodeSlugAsync(
        AppDbContext db, string title, string? parentTitle, CancellationToken ct, ISet<string>? batch = null)
    {
        var slug = Truncate(Slugs.FromText(title));
        if (slug.Length == 0)
            slug = "baslik";
        if (!Reserved.Contains(slug) && !await IsTakenAsync(db, slug, ct, batch))
            return Remember(slug, batch);
        if (parentTitle is not null && Truncate(Slugs.FromText(parentTitle)) is { Length: > 0 } parentSlug)
        {
            var prefixed = Truncate($"{parentSlug}-{slug}");
            if (!await IsTakenAsync(db, prefixed, ct, batch))
                return Remember(prefixed, batch);
            slug = prefixed;
        }
        else if (Reserved.Contains(slug))
            slug = $"{slug}-baslik";
        return Remember(await NumberedAsync(db, slug, ct, batch), batch);
    }

    private static string Remember(string slug, ISet<string>? batch)
    {
        batch?.Add(slug);
        return slug;
    }

    private static async Task<bool> IsTakenAsync(AppDbContext db, string slug, CancellationToken ct, ISet<string>? batch) =>
        batch?.Contains(slug) == true
        || await db.LectureNotes.AnyAsync(n => n.Slug == slug, ct) || await db.LectureCourses.AnyAsync(c => c.Slug == slug, ct);

    private static async Task<string> NumberedAsync(AppDbContext db, string slug, CancellationToken ct, ISet<string>? batch = null)
    {
        var prefix = slug + "-";
        var taken = (await db.LectureNotes.Where(n => n.Slug == slug || n.Slug.StartsWith(prefix)).Select(n => n.Slug).ToListAsync(ct))
            .Concat(await db.LectureCourses.Where(c => c.Slug == slug || c.Slug.StartsWith(prefix)).Select(c => c.Slug).ToListAsync(ct))
            .ToHashSet();
        if (batch is not null)
            taken.UnionWith(batch);
        var candidate = slug;
        for (var i = 2; taken.Contains(candidate) || Reserved.Contains(candidate); i++)
            candidate = $"{slug}-{i}";
        return candidate;
    }

    private static string Truncate(string slug) => slug.Length > MaxSlugLength ? slug[..MaxSlugLength].TrimEnd('-') : slug;
}
