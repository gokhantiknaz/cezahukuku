using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Content;

/// <param name="Text">Başlık listesi (yapıştırılan ya da dosyadan alınan metin).</param>
/// <param name="ApplySuggestions">Metin son önizlemeden beri değişti: metinden önerilen ad ve bölüm numarası kullanılsın.</param>
public sealed record LectureImportPreviewRequest(string? Text, string? Title, int? ChapterNumber, bool ApplySuggestions);

/// <param name="Line">Kaynak metindeki satır (1'den).</param>
/// <param name="SourceNumber">Satırdaki numara (kaydedilmez).</param>
/// <param name="Number">Sistemin vereceği numara.</param>
/// <param name="Label">Gösterilecek numara ("1. Bölüm", "2.1").</param>
public sealed record LectureImportHeading(int Line, string Title, int Depth, string SourceNumber, string Number, string Label);

/// <param name="Line">Kaynak metindeki satır; metnin ya da formun tamamıyla ilgiliyse 0.</param>
public sealed record LectureImportIssue(int Line, string Message);

/// <summary>Önizleme: kullanılan ad ve bölüm numarası (öneriler uygulandıysa öneriden), ağaç, hatalar, uyarılar.</summary>
/// <param name="Errors">Boş değilse ders kurulamaz.</param>
public sealed record LectureImportPreview(
    string Title,
    int? ChapterNumber,
    string? SuggestedTitle,
    int? SuggestedChapterNumber,
    List<LectureImportHeading> Headings,
    List<LectureImportIssue> Errors,
    List<LectureImportIssue> Warnings);

public sealed record LectureImportRequest(string? Text, string? Title, int? ChapterNumber, Visibility Visibility);

/// <param name="Slug">Yeni dersin adresi (/ders-notlari/{slug}).</param>
public sealed record LectureImportResult(Guid CourseId, string Slug, int HeadingCount);

/// <summary>
/// Spec 0004: ders başlıklarını toplu içe aktarma. Önizleme hiçbir şey kaydetmez; kayıt metni yeniden okur (istemcinin
/// ağacına güvenmez), hata varsa hiçbir şey oluşturmaz, yoksa yeni dersi ve bütün başlıklarını tek kayıtta (tek işlem)
/// kurar. Yalnız yönetici. Dosyadan metin alma kanun içe aktarmanın uç noktasıyladır (/api/admin/import/extract-text).
/// </summary>
public static class LectureImportEndpoints
{
    public static void MapLectureImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/lecture-courses/import").WithTags("LectureCourses").RequireAuthorization(Policies.AdminOnly);
        group.MapPost("/preview", Preview).WithName("PreviewLectureImport");
        group.MapPost("/", ImportAsync).WithName("ImportLectureCourse");
    }

    private static Ok<LectureImportPreview> Preview(LectureImportPreviewRequest request) =>
        TypedResults.Ok(ToPreview(Read(request.Text, request.Title, request.ChapterNumber, request.ApplySuggestions)));

    private static async Task<Results<Created<LectureImportResult>, ValidationProblem>> ImportAsync(
        LectureImportRequest request, AppDbContext db, CancellationToken ct)
    {
        var import = Read(request.Text, request.Title, request.ChapterNumber, applySuggestions: false);
        if (import.Errors.Count > 0)
        {
            var errors = new ValidationErrors();
            foreach (var error in import.Errors)
                errors.Add(error.Field, error.Line > 0 ? $"Satır {error.Line}: {error.Message}" : error.Message);
            return errors.ToProblem();
        }

        var batch = new HashSet<string>();
        var course = new LectureCourse
        {
            Title = import.Result.Title,
            Visibility = request.Visibility,
            ChapterNumber = import.Result.ChapterNumber,
            SortOrder = (await db.LectureCourses.MaxAsync(c => (int?)c.SortOrder, ct) ?? -1) + 1,
        };
        course.Slug = await LectureTree.UniqueSlugAsync(db, course.Title, "ders", ct, batch);
        db.LectureCourses.Add(course);

        // Her düzeyin son başlığı: sıradaki başlığın üst başlığı bir üst düzeyin sonuncusudur.
        var lastAtDepth = new List<LectureNote>();
        foreach (var heading in import.Result.Headings)
        {
            var parent = heading.Depth > 1 ? lastAtDepth[heading.Depth - 2] : null;
            var note = new LectureNote
            {
                Title = heading.Title,
                Visibility = Visibility.Public,
                CourseId = course.Id,
                ParentId = parent?.Id,
                SortOrder = int.Parse(heading.Number.Split('.')[^1], System.Globalization.CultureInfo.InvariantCulture) - 1,
                Slug = await LectureTree.NodeSlugAsync(db, heading.Title, parent?.Title, ct, batch),
            };
            db.LectureNotes.Add(note);
            lastAtDepth.RemoveRange(heading.Depth - 1, lastAtDepth.Count - (heading.Depth - 1));
            lastAtDepth.Add(note);
        }
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/lecture-courses/{course.Slug}",
            new LectureImportResult(course.Id, course.Slug, import.Result.Headings.Count));
    }

    private sealed record Issue(int Line, string Message, string Field);

    private sealed record Reading(HeadingImport Result, List<Issue> Errors);

    /// <summary>Okuyucunun sonucu + formun kendi denetimleri (bölüm numarası aralığı).</summary>
    private static Reading Read(string? text, string? title, int? chapterNumber, bool applySuggestions)
    {
        var result = LectureHeadingParser.Parse(text, title, chapterNumber, applySuggestions);
        var errors = result.Errors
            .Select(e => new Issue(e.Line, e.Message, e.Line == 0 && e.Message.StartsWith("Ders adı", StringComparison.Ordinal) ? "title" : "text"))
            .ToList();
        if (result.ChapterNumber is < LectureCourse.MinChapterNumber or > LectureCourse.MaxChapterNumber)
            errors.Insert(0, new(0, $"Bölüm numarası {LectureCourse.MinChapterNumber}–{LectureCourse.MaxChapterNumber} arası olmalı.", "chapterNumber"));
        return new(result, errors);
    }

    private static LectureImportPreview ToPreview(Reading reading)
    {
        var result = reading.Result;
        var chaptered = result.ChapterNumber is not null;
        return new(result.Title, result.ChapterNumber, result.SuggestedTitle, result.SuggestedChapterNumber,
            [.. result.Headings.Select(h => new LectureImportHeading(h.Line, h.Title, h.Depth, h.SourceNumber, h.Number,
                LectureOutline.LabelOf(h.Number, h.Depth, chaptered)))],
            [.. reading.Errors.Select(e => new LectureImportIssue(e.Line, e.Message))],
            [.. result.Warnings.Select(w => new LectureImportIssue(w.Line, w.Message))]);
    }
}
