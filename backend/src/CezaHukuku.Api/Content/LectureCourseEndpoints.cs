using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Content;

/// <param name="NodeCount">Okuyanın görebildiği başlık sayısı.</param>
public sealed record LectureCourseSummary(
    Guid Id, string Slug, string Title, string Description, Visibility Visibility, int NodeCount);

/// <summary>İçindekilerde bir başlık. Liste içindekilerdeki sıradadır (önce başlık, sonra alt başlıkları).</summary>
/// <param name="ParentId">Üst başlık; birinci düzeyde boş.</param>
/// <param name="Number">Ağaçtaki yerden hesaplanan numara ("1.5.1"); okuyandan bağımsızdır.</param>
/// <param name="Label">Gösterilen numara: birinci düzeyde "1. Bölüm", altında numaranın kendisi.</param>
/// <param name="Depth">Düzey (birinci düzey 1).</param>
/// <param name="Visibility">Başlığın kendi ayarı (düzenleme için).</param>
/// <param name="EffectiveVisibility">Ders ve üst başlıklarla daralmış görünürlük ("Özel" rozeti için).</param>
public sealed record LectureTocEntry(
    Guid Id, Guid? ParentId, string Slug, string Title, string Number, string Label, int Depth,
    Visibility Visibility, Visibility EffectiveVisibility);

public sealed record LectureCourseDetail(
    Guid Id, string Slug, string Title, string Description, Visibility Visibility, List<LectureTocEntry> Toc);

/// <param name="Description">İsteğe bağlı kısa açıklama (düz metin).</param>
public sealed record SaveLectureCourseRequest(string Title, string? Description, Visibility Visibility);

/// <param name="CourseIds">Bütün dersler, istenen sırayla.</param>
public sealed record ReorderLectureCoursesRequest(List<Guid> CourseIds);

/// <param name="ParentId">Üst başlık (aynı dersten); boşsa birinci düzeye. Başlık kardeşlerinin sonuna eklenir.</param>
public sealed record AddLectureNodeRequest(string Title, Guid? ParentId, Visibility Visibility);

/// <param name="ParentId">Yeni üst başlık (aynı dersten); boşsa birinci düzey.</param>
/// <param name="Index">Yeni kardeşleri arasındaki yeri (0 = en başa); kardeş sayısından büyükse sona.</param>
public sealed record MoveLectureNodeRequest(Guid? ParentId, int Index);

/// <summary>
/// Dersler ve başlık ağaçları. Herkes okur (özel ders ve başlıklar yalnız girişte); yönetici ders ve başlık ekler,
/// düzenler, taşır ve siler. Başlığın metni, belgeleri ve madde bağlantısı ders notu uç noktalarıyla
/// (<see cref="LectureNoteEndpoints"/>) okunur ve düzenlenir.
/// </summary>
public static class LectureCourseEndpoints
{
    public static void MapLectureCourseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lecture-courses").WithTags("LectureCourses").AllowAnonymous();
        group.MapGet("/", ListAsync).WithName("ListLectureCourses");
        group.MapGet("/{slug}", GetAsync).WithName("GetLectureCourse");

        var admin = app.MapGroup("/api/admin/lecture-courses").WithTags("LectureCourses").RequireAuthorization(Policies.AdminOnly);
        admin.MapPost("/", CreateAsync).WithName("CreateLectureCourse");
        admin.MapPut("/order", ReorderAsync).WithName("ReorderLectureCourses");
        admin.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateLectureCourse");
        admin.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteLectureCourse");
        admin.MapPost("/{id:guid}/nodes", AddNodeAsync).WithName("AddLectureNode");

        app.MapPut("/api/admin/lecture-notes/{id:guid}/position", MoveNodeAsync).WithName("MoveLectureNode")
            .WithTags("LectureCourses").RequireAuthorization(Policies.AdminOnly);
    }

    /// <summary>Dersler sırasıyla.</summary>
    private static async Task<Ok<List<LectureCourseSummary>>> ListAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var includePrivate = principal.CanSeePrivate();
        var courses = await db.LectureCourses.AsNoTracking()
            .Where(c => includePrivate || c.Visibility == Visibility.Public)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
            .ToListAsync(ct);
        var outlines = await LectureTree.OutlinesAsync(db, courses.Select(c => c.Id), includePrivate, ct);
        return TypedResults.Ok(courses
            .Select(c => new LectureCourseSummary(c.Id, c.Slug, c.Title, c.Description, c.Visibility, outlines[c.Id].TocOrder.Count))
            .ToList());
    }

    /// <summary>Ders ve içindekileri (okuyanın görebildiği başlıklar).</summary>
    private static async Task<Results<Ok<LectureCourseDetail>, NotFound>> GetAsync(
        string slug, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var includePrivate = principal.CanSeePrivate();
        var course = await db.LectureCourses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug.ToLowerInvariant() && (includePrivate || c.Visibility == Visibility.Public), ct);
        if (course is null)
            return TypedResults.NotFound();
        return TypedResults.Ok(await DetailAsync(db, course, includePrivate, ct));
    }

    private static async Task<Results<Created<LectureCourseDetail>, ValidationProblem>> CreateAsync(
        SaveLectureCourseRequest request, AppDbContext db, CancellationToken ct)
    {
        if (Validate(request) is { } problem)
            return problem;
        var course = new LectureCourse
        {
            SortOrder = (await db.LectureCourses.MaxAsync(c => (int?)c.SortOrder, ct) ?? -1) + 1,
        };
        Apply(course, request);
        course.Slug = await LectureTree.UniqueSlugAsync(db, course.Title, "ders", ct);
        db.LectureCourses.Add(course);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/lecture-courses/{course.Slug}", await DetailAsync(db, course, includePrivate: true, ct));
    }

    /// <summary>Adres değişmez: ad değişse de verilen bağlantılar çalışır.</summary>
    private static async Task<Results<Ok<LectureCourseDetail>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, SaveLectureCourseRequest request, AppDbContext db, CancellationToken ct)
    {
        var course = await db.LectureCourses.FindAsync([id], ct);
        if (course is null)
            return TypedResults.NotFound();
        if (Validate(request) is { } problem)
            return problem;
        Apply(course, request);
        // Görünürlük değişince başlıkların etkin görünürlüğü de değişir; arama indeksi dersin tamamını yeniden yazar.
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await DetailAsync(db, course, includePrivate: true, ct));
    }

    private static async Task<Results<NoContent, ValidationProblem>> ReorderAsync(
        ReorderLectureCoursesRequest request, AppDbContext db, CancellationToken ct)
    {
        var courses = await db.LectureCourses.ToListAsync(ct);
        if (request.CourseIds.Count != courses.Count || request.CourseIds.Distinct().Count() != courses.Count
            || !courses.All(c => request.CourseIds.Contains(c.Id)))
            return ValidationErrors.Single("courseIds", "Sıra, bütün dersleri birer kez içermeli.");
        foreach (var course in courses)
            course.SortOrder = request.CourseIds.IndexOf(course.Id);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Kayıt saklanır, okuyucudan kalkar. Başlığı olan ders silinmez (önce başlıklar).</summary>
    private static async Task<Results<NoContent, NotFound, ValidationProblem>> DeleteAsync(
        Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var course = await db.LectureCourses.FindAsync([id], ct);
        if (course is null)
            return TypedResults.NotFound();
        if (await db.LectureNotes.AnyAsync(n => n.CourseId == id, ct))
            return ValidationErrors.Single("id", "Başlığı olan ders silinemez; önce başlıklarını silin.");
        course.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Derse başlık ekler: birinci düzeye ya da verilen başlığın altına, kardeşlerinin sonuna.</summary>
    private static async Task<Results<Created<LectureNoteDetail>, NotFound, ValidationProblem>> AddNodeAsync(
        Guid id, AddLectureNodeRequest request, AppDbContext db, CancellationToken ct)
    {
        if (!await db.LectureCourses.AnyAsync(c => c.Id == id, ct))
            return TypedResults.NotFound();
        var errors = new ValidationErrors().Required("title", request.Title, LectureNote.MaxTitleLength);
        LectureNote? parent = null;
        if (request.ParentId is { } parentId)
        {
            parent = await db.LectureNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == parentId && n.CourseId == id, ct);
            if (parent is null)
                errors.Add("parentId", "Üst başlık bu derste bulunamadı.");
        }
        if (errors.HasErrors)
            return errors.ToProblem();

        var title = InputText.CleanRequired(request.Title);
        var note = new LectureNote
        {
            Title = title,
            Visibility = request.Visibility,
            CourseId = id,
            ParentId = parent?.Id,
            SortOrder = (await db.LectureNotes.Where(n => n.CourseId == id && n.ParentId == request.ParentId)
                .MaxAsync(n => (int?)n.SortOrder, ct) ?? -1) + 1,
            Slug = await LectureTree.NodeSlugAsync(db, title, parent?.Title, ct),
        };
        db.LectureNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/lecture-notes/{note.Slug}", (await LectureNoteEndpoints.DetailAsync(db, note, includePrivate: true, ct))!);
    }

    /// <summary>
    /// Başlığı aynı dersin başka bir yerine taşır: yeni üst başlık (ya da birinci düzey) ve kardeşler arasındaki yeri.
    /// Aynı düzeyde yukarı/aşağı da budur. Başlık kendisinin ya da alt başlıklarından birinin altına taşınamaz.
    /// Adres değişmez; numaralar ağaçtan yeniden hesaplanır.
    /// </summary>
    private static async Task<Results<Ok<LectureNoteDetail>, NotFound, ValidationProblem>> MoveNodeAsync(
        Guid id, MoveLectureNodeRequest request, AppDbContext db, CancellationToken ct)
    {
        var note = await db.LectureNotes.FindAsync([id], ct);
        if (note is null)
            return TypedResults.NotFound();
        if (note.CourseId is not { } courseId)
            return ValidationErrors.Single("id", "Bir derse bağlı olmayan ders notu taşınamaz.");
        if (request.Index < 0)
            return ValidationErrors.Single("index", "Sıra negatif olamaz.");

        var courseNodes = await LectureTree.NodesAsync(db, [courseId], ct);
        if (request.ParentId is { } parentId && courseNodes.All(n => n.Id != parentId))
            return ValidationErrors.Single("parentId", "Üst başlık bu derste bulunamadı.");
        if (LectureOutline.WouldCreateCycle(courseNodes, id, request.ParentId))
            return ValidationErrors.Single("parentId", "Başlık kendisinin ya da alt başlıklarından birinin altına taşınamaz.");

        var siblings = await db.LectureNotes
            .Where(n => n.CourseId == courseId && n.ParentId == request.ParentId && n.Id != id)
            .OrderBy(n => n.SortOrder).ThenBy(n => n.CreatedAt).ThenBy(n => n.Id)
            .ToListAsync(ct);
        siblings.Insert(Math.Min(request.Index, siblings.Count), note);
        note.ParentId = request.ParentId;
        for (var i = 0; i < siblings.Count; i++)
            siblings[i].SortOrder = i;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await LectureNoteEndpoints.DetailAsync(db, note, includePrivate: true, ct))!);
    }

    private static ValidationProblem? Validate(SaveLectureCourseRequest request)
    {
        var errors = new ValidationErrors()
            .Required("title", request.Title, LectureCourse.MaxTitleLength)
            .MaxLength("description", request.Description, LectureCourse.MaxDescriptionLength);
        return errors.HasErrors ? errors.ToProblem() : null;
    }

    private static void Apply(LectureCourse course, SaveLectureCourseRequest request)
    {
        course.Title = InputText.CleanRequired(request.Title);
        course.Description = InputText.Clean(request.Description) ?? "";
        course.Visibility = request.Visibility;
    }

    private static async Task<LectureCourseDetail> DetailAsync(
        AppDbContext db, LectureCourse course, bool includePrivate, CancellationToken ct)
    {
        var nodes = await LectureTree.NodesAsync(db, [course.Id], ct);
        var outline = LectureOutline.Build(course, nodes, includePrivate);
        return new(course.Id, course.Slug, course.Title, course.Description, course.Visibility,
            [.. outline.TocOrder.Select(n => new LectureTocEntry(
                n.Note.Id,
                n.Note.ParentId is { } parentId && outline.Find(parentId) is not null ? parentId : null,
                n.Note.Slug, n.Note.Title, n.Number, n.Label, n.Depth, n.Note.Visibility, n.EffectiveVisibility))]);
    }
}
