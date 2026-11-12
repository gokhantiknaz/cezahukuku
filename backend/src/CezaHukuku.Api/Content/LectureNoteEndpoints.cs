using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Attachments;
using CezaHukuku.Infrastructure.Files;
using CezaHukuku.Infrastructure.Content;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Content;

public sealed record LawGroupRef(Guid Id, string Slug, string Title);

public sealed record LectureNoteLawRef(Guid Id, string Slug, string Label, string Name);

/// <summary>Ders notunun bağlı olduğu yer; hepsi boşsa genel ders notu. Okuyanın göremediği kanun/madde gösterilmez.</summary>
public sealed record LectureNoteScope(LawGroupRef? Group, LectureNoteLawRef? Law, LinkedArticleRef? Article);

/// <param name="Excerpt">Metnin başı (liste için).</param>
/// <param name="FileCount">Ekli belge sayısı.</param>
public sealed record LectureNoteSummary(
    Guid Id, string Slug, string Title, string Excerpt, LectureNoteScope Scope, Visibility Visibility, DateTimeOffset UpdatedAt,
    int FileCount);

/// <param name="Url">Dosyanın adresi (PDF ve resim tarayıcıda açılır, diğerleri iner); "?download=true" her zaman indirir.</param>
/// <param name="PageCount">PDF sayfa sayısı.</param>
/// <param name="HasText">Metni okunabildi (aramada bulunur); taranmış PDF ve resimde false.</param>
public sealed record LectureNoteFileDto(
    Guid Id, string Title, string FileName, string ContentType, long SizeBytes, int? PageCount, bool HasText, string Url,
    DateTimeOffset CreatedAt);

/// <param name="Title">Belgenin görünen adı.</param>
public sealed record UpdateLectureNoteFileRequest(string Title);

/// <param name="FileIds">Ders notunun bütün belgeleri, istenen sırayla.</param>
public sealed record ReorderLectureNoteFilesRequest(List<Guid> FileIds);

/// <param name="LawGroupId">Düzenleme formu için bağlantının kimlikleri (kapsamdaki adlar okuyana göre süzülür).</param>
public sealed record LectureNoteDetail(
    Guid Id,
    string Slug,
    string Title,
    string Html,
    LectureNoteScope Scope,
    Visibility Visibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? LawGroupId,
    Guid? LawId,
    Guid? ArticleId,
    List<LectureNoteFileDto> Files,
    LectureNodeContext? Node);

/// <summary>Maddenin altında gösterilen ders notu bağlantısı.</summary>
/// <param name="Url">Okuma sayfası: düz notta /ders-notlari/{not}, başlıkta /ders-notlari/{ders}/{başlık}.</param>
/// <param name="Label">Başlığın numarası ("1.5.1", birinci düzeyde "1. Bölüm"); düz notta boş.</param>
/// <param name="CourseTitle">Başlığın dersi; düz notta boş.</param>
public sealed record LectureNoteRef(string Slug, string Title, string Url, string? Label, string? CourseTitle);

public sealed record LectureCourseRef(Guid Id, string Slug, string Title);

/// <param name="Label">Gösterilen numara: birinci düzeyde "1. Bölüm", altında "1.5.1".</param>
/// <param name="EffectiveVisibility">Ders ve üst başlıklarla daralmış görünürlük ("Özel" rozeti için).</param>
public sealed record LectureNodeRef(Guid Id, string Slug, string Title, string Label, Visibility EffectiveVisibility);

/// <summary>Başlığın dersteki yeri: ders yolu, numara, önceki/sonraki ve alt başlıklar (okuyanın görebildikleri).</summary>
/// <param name="Path">Birinci düzeyden üst başlığa kadar (başlığın kendisi hariç).</param>
public sealed record LectureNodeContext(
    LectureCourseRef Course,
    Guid? ParentId,
    string Number,
    string Label,
    int Depth,
    Visibility EffectiveVisibility,
    List<LectureNodeRef> Path,
    LectureNodeRef? Previous,
    LectureNodeRef? Next,
    List<LectureNodeRef> Children);

/// <param name="LawGroupId">İsteğe bağlı: kanun grubu (Temel Ceza Kanunları…).</param>
/// <param name="LawId">İsteğe bağlı: kanun; verilirse grubu kanunun grubu olur.</param>
/// <param name="ArticleId">İsteğe bağlı: madde; verilirse kanunu ve grubu maddeninki olur.</param>
public sealed record SaveLectureNoteRequest(
    string Title, string? Html, Visibility Visibility, Guid? LawGroupId, Guid? LawId, Guid? ArticleId);

/// <summary>
/// Ders Notları: kanundan bağımsız konu anlatımları. Herkes okur (private not yalnız girişte), yönetici girer.
/// İsteğe bağlı olarak kanun grubuna, kanuna ya da maddeye bağlanır. Bir dersin başlıkları da ders notudur
/// (<see cref="LectureCourseEndpoints"/>): okuma, belge ve madde bağlantısında görünürlükleri dersin ağacından
/// gelir (<see cref="LectureTree.IsVisible"/>).
/// </summary>
public static class LectureNoteEndpoints
{
    private const int MaxHtmlLength = 400_000;
    private const int ExcerptLength = 240;

    public static void MapLectureNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lecture-notes").WithTags("LectureNotes").AllowAnonymous();
        group.MapGet("/", ListAsync).WithName("ListLectureNotes");
        group.MapGet("/{slug}", GetAsync).WithName("GetLectureNote");
        group.MapGet("/{slug}/files/{fileId:guid}", FileAsync).WithName("GetLectureNoteFile");

        var admin = app.MapGroup("/api/admin/lecture-notes").WithTags("LectureNotes").RequireAuthorization(Policies.AdminOnly);
        admin.MapPost("/", CreateAsync).WithName("CreateLectureNote");
        admin.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateLectureNote");
        admin.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteLectureNote");
        admin.MapPost("/{id:guid}/files", UploadFileAsync).WithName("UploadLectureNoteFile")
            // Çok parçalı form; SameSite=Lax oturum çerezi başka siteden gönderilen formda taşınmaz.
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LectureNoteFile.MaxBytes + 1024 * 1024));
        admin.MapPut("/{id:guid}/files/order", ReorderFilesAsync).WithName("ReorderLectureNoteFiles");
        admin.MapPut("/{id:guid}/files/{fileId:guid}", UpdateFileAsync).WithName("UpdateLectureNoteFile");
        admin.MapDelete("/{id:guid}/files/{fileId:guid}", DeleteFileAsync).WithName("DeleteLectureNoteFile");
    }

    /// <summary>
    /// Düz ders notları (bir derse bağlı olmayanlar), son güncellenen önce. Süzme (kapsam, metin) istemcide yapılır;
    /// notlar az sayıdadır.
    /// </summary>
    private static async Task<Ok<List<LectureNoteSummary>>> ListAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var includePrivate = principal.CanSeePrivate();
        var notes = await db.LectureNotes.AsNoTracking()
            .Where(n => n.CourseId == null && (includePrivate || n.Visibility == Visibility.Public))
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(ct);
        var scopes = await ScopesAsync(db, notes, includePrivate, ct);
        var noteIds = notes.Select(n => n.Id).ToList();
        var fileCounts = await db.LectureNoteFiles.Where(f => noteIds.Contains(f.LectureNoteId))
            .GroupBy(f => f.LectureNoteId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return TypedResults.Ok(notes
            .Select(n => new LectureNoteSummary(n.Id, n.Slug, n.Title, Excerpt(n.PlainText), scopes[n.Id], n.Visibility, n.UpdatedAt,
                fileCounts.GetValueOrDefault(n.Id)))
            .ToList());
    }

    private static async Task<Results<Ok<LectureNoteDetail>, NotFound>> GetAsync(
        string slug, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var includePrivate = principal.CanSeePrivate();
        var note = await db.LectureNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Slug == slug.ToLowerInvariant(), ct);
        return note is not null && await DetailAsync(db, note, includePrivate, ct) is { } detail
            ? TypedResults.Ok(detail)
            : TypedResults.NotFound();
    }

    private static async Task<Results<Created<LectureNoteDetail>, ValidationProblem>> CreateAsync(
        SaveLectureNoteRequest request, AppDbContext db, CancellationToken ct)
    {
        var note = new LectureNote();
        if (await ApplyAsync(note, request, db, ct) is { } problem)
            return problem;
        note.Slug = await LectureTree.UniqueSlugAsync(db, note.Title, "ders-notu", ct);
        db.LectureNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/lecture-notes/{note.Slug}", (await DetailAsync(db, note, includePrivate: true, ct))!);
    }

    /// <summary>Adres (slug) değişmez: başlık değişse de verilen bağlantılar çalışmaya devam eder.</summary>
    private static async Task<Results<Ok<LectureNoteDetail>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, SaveLectureNoteRequest request, AppDbContext db, CancellationToken ct)
    {
        var note = await db.LectureNotes.FindAsync([id], ct);
        if (note is null)
            return TypedResults.NotFound();
        if (await ApplyAsync(note, request, db, ct) is { } problem)
            return problem;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await DetailAsync(db, note, includePrivate: true, ct))!);
    }

    /// <summary>Kayıt ve belgeleri saklanır, okuyucudan kalkar. Alt başlığı olan başlık silinmez (önce alt başlıklar).</summary>
    private static async Task<Results<NoContent, NotFound, ValidationProblem>> DeleteAsync(
        Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var note = await db.LectureNotes.FindAsync([id], ct);
        if (note is null)
            return TypedResults.NotFound();
        if (await db.LectureNotes.AnyAsync(n => n.ParentId == id, ct))
            return ValidationErrors.Single("id", "Alt başlığı olan başlık silinemez; önce alt başlıkları taşıyın ya da silin.");
        note.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Maddeye bağlı, okuyanın görebileceği ders notları ve ders başlıkları (madde ekranında): önce düz notlar, sonra
    /// başlıklar dersine ve adına göre; başlık numarası ve dersiyle, dersteki adresine bağlanır.
    /// </summary>
    public static async Task<List<LectureNoteRef>> ForArticleAsync(AppDbContext db, Guid articleId, bool includePrivate)
    {
        var notes = await db.LectureNotes.AsNoTracking()
            .Where(n => n.ArticleId == articleId)
            .Select(n => new LectureNote { Id = n.Id, Slug = n.Slug, Title = n.Title, Visibility = n.Visibility, CourseId = n.CourseId })
            .ToListAsync();
        var outlines = await LectureTree.OutlinesAsync(
            db, notes.Where(n => n.CourseId != null).Select(n => n.CourseId!.Value), includePrivate, CancellationToken.None);
        return [.. notes
            .Where(n => LectureTree.IsVisible(n, outlines, includePrivate))
            .Select(n => n.CourseId is { } courseId && outlines[courseId] is var outline
                ? new LectureNoteRef(n.Slug, n.Title, LectureTree.UrlOf(n, outline.Course), outline.Find(n.Id)!.Label, outline.Course.Title)
                : new LectureNoteRef(n.Slug, n.Title, LectureTree.UrlOf(n, null), null, null))
            .OrderBy(r => r.CourseTitle is null ? 0 : 1).ThenBy(r => r.CourseTitle).ThenBy(r => r.Title)];
    }

    private static async Task<ValidationProblem?> ApplyAsync(
        LectureNote note, SaveLectureNoteRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = new ValidationErrors().Required("title", request.Title, LectureNote.MaxTitleLength);
        if (request.Html is { Length: > MaxHtmlLength })
            errors.Add("html", "Metin çok uzun.");
        // Metin boş olabilir: ders notu yalnız ekli belgelerden oluşabilir (belgeler not kaydedildikten sonra eklenir).
        var html = RichText.Sanitize(request.Html);

        // Bağlantı en dar seçimden çıkarılır: madde → kanunu → grubu.
        Guid? groupId = request.LawGroupId, lawId = request.LawId, articleId = request.ArticleId;
        if (articleId is { } aid)
        {
            var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aid, ct);
            if (article is null)
                errors.Add("articleId", "Madde bulunamadı.");
            else
                lawId = article.LawId;
        }
        if (lawId is { } lid)
        {
            var law = await db.Laws.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lid, ct);
            if (law is null)
                errors.Add("lawId", "Kanun bulunamadı.");
            else
                groupId = law.GroupId;
        }
        if (groupId is { } gid && !await db.LawGroups.AnyAsync(g => g.Id == gid, ct))
            errors.Add("lawGroupId", "Kanun grubu bulunamadı.");

        if (errors.HasErrors)
            return errors.ToProblem();

        note.Title = InputText.CleanRequired(request.Title);
        note.Html = html;
        note.PlainText = RichText.ToPlainText(html);
        note.Visibility = request.Visibility;
        (note.LawGroupId, note.LawId, note.ArticleId) = (groupId, lawId, articleId);
        return null;
    }

    /// <summary>Notların bağlantıları adlarıyla; okuyanın göremediği kanun ve madde boş bırakılır.</summary>
    private static async Task<Dictionary<Guid, LectureNoteScope>> ScopesAsync(
        AppDbContext db, IReadOnlyCollection<LectureNote> notes, bool includePrivate, CancellationToken ct)
    {
        var groupIds = notes.Where(n => n.LawGroupId != null).Select(n => n.LawGroupId!.Value).Distinct().ToList();
        var groups = await db.LawGroups.AsNoTracking().Where(g => groupIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, ct);
        var lawIds = notes.Where(n => n.LawId != null).Select(n => n.LawId!.Value).Distinct().ToList();
        var laws = await db.Laws.AsNoTracking()
            .Where(l => lawIds.Contains(l.Id) && (includePrivate || l.Visibility == Visibility.Public))
            .ToDictionaryAsync(l => l.Id, ct);
        var articleIds = notes.Where(n => n.ArticleId != null).Select(n => n.ArticleId!.Value).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToListAsync(ct);
        var visibleArticles = await VisibleArticles.FilterAsync(db, articles, includePrivate);

        return notes.ToDictionary(n => n.Id, n => new LectureNoteScope(
            n.LawGroupId is { } g && groups.TryGetValue(g, out var group) ? new LawGroupRef(group.Id, group.Slug, group.Title) : null,
            n.LawId is { } l && laws.TryGetValue(l, out var law) ? new LectureNoteLawRef(law.Id, law.Slug, law.ShortName ?? law.Number, law.Name) : null,
            n.ArticleId is { } a && visibleArticles.TryGetValue(a, out var article) ? VisibleArticles.ToRef(article.Article, article.Law) : null));
    }

    private static string Excerpt(string text)
    {
        var flat = string.Join(' ', text.Split((char[])['\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= ExcerptLength ? flat : flat[..ExcerptLength].TrimEnd() + "…";
    }

    /// <summary>
    /// Ders notunun okuyana göre ayrıntısı; başlıkta dersteki yeriyle. Okuyan göremiyorsa (özel not, özel ders ya da
    /// özel üst başlığın altı) null.
    /// </summary>
    internal static async Task<LectureNoteDetail?> DetailAsync(AppDbContext db, LectureNote n, bool includePrivate, CancellationToken ct)
    {
        LectureNodeContext? context = null;
        if (n.CourseId is { } courseId)
        {
            var outline = await LectureTree.OutlineAsync(db, courseId, includePrivate, ct);
            if (outline?.Find(n.Id) is not { } node)
                return null;
            context = ContextOf(outline, node);
        }
        else if (!includePrivate && n.Visibility != Visibility.Public)
            return null;

        var scopes = await ScopesAsync(db, [n], includePrivate, ct);
        var files = await db.LectureNoteFiles.AsNoTracking().Where(f => f.LectureNoteId == n.Id)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.CreatedAt).ToListAsync(ct);
        return new(n.Id, n.Slug, n.Title, n.Html, scopes[n.Id], n.Visibility, n.CreatedAt, n.UpdatedAt, n.LawGroupId, n.LawId, n.ArticleId,
            [.. files.Select(f => ToFileDto(n, f))], context);
    }

    private static LectureNodeContext ContextOf(LectureOutline outline, LectureOutlineNode node)
    {
        static LectureNodeRef Ref(LectureOutlineNode n) => new(n.Note.Id, n.Note.Slug, n.Note.Title, n.Label, n.EffectiveVisibility);
        var course = outline.Course;
        return new(
            new LectureCourseRef(course.Id, course.Slug, course.Title),
            node.Note.ParentId is { } parentId && outline.Find(parentId) is not null ? parentId : null,
            node.Number,
            node.Label,
            node.Depth,
            node.EffectiveVisibility,
            [.. outline.PathOf(node.Note.Id).Select(Ref)],
            outline.Previous(node.Note.Id) is { } previous ? Ref(previous) : null,
            outline.Next(node.Note.Id) is { } next ? Ref(next) : null,
            [.. node.Children.Select(Ref)]);
    }

    private static LectureNoteFileDto ToFileDto(LectureNote note, LectureNoteFile f) =>
        new(f.Id, f.Title, f.FileName, f.ContentType, f.SizeBytes, f.PageCount, f.Text.Length > 0,
            $"/api/lecture-notes/{note.Slug}/files/{f.Id}", f.CreatedAt);

    /// <summary>Belgenin kendisi: notu görebilen açar. PDF ve resim tarayıcıda açılır, diğerleri iner.</summary>
    /// <param name="download">true: her türü indirilecek dosya olarak (özgün adıyla).</param>
    private static async Task<Results<FileStreamHttpResult, NotFound>> FileAsync(
        string slug, Guid fileId, bool? download, ClaimsPrincipal principal, AppDbContext db, IFileStorage storage,
        HttpContext http, CancellationToken ct)
    {
        var includePrivate = principal.CanSeePrivate();
        var lowerSlug = slug.ToLowerInvariant();
        var found = await (
                from f in db.LectureNoteFiles
                join n in db.LectureNotes on f.LectureNoteId equals n.Id
                where f.Id == fileId && n.Slug == lowerSlug
                select new { File = f, Note = n })
            .AsNoTracking().FirstOrDefaultAsync(ct);
        if (found is null || !await LectureTree.IsVisibleAsync(db, found.Note, includePrivate, ct)
            || await storage.OpenReadAsync(found.File.StorageKey, ct) is not { } stream)
            return TypedResults.NotFound();
        var file = found.File;

        if (download != true && DocumentFiles.IsInline(file.ContentType))
        {
            // Tarayıcıda açılır; "Farklı kaydet" yine özgün adı önerir.
            http.Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = file.FileName }.ToString();
            return TypedResults.Stream(stream, file.ContentType, enableRangeProcessing: true);
        }
        return TypedResults.Stream(stream, file.ContentType, fileDownloadName: file.FileName, enableRangeProcessing: true);
    }

    /// <summary>Ders notuna belge ekler (sona). Metni hemen çıkarılır ve aranır.</summary>
    /// <param name="title">Görünen ad; verilmezse dosya adı (uzantısız).</param>
    private static async Task<Results<Created<LectureNoteFileDto>, NotFound, ValidationProblem>> UploadFileAsync(
        Guid id, IFormFile file, [FromForm] string? title, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var note = await db.LectureNotes.FindAsync([id], ct);
        if (note is null)
            return TypedResults.NotFound();

        var errors = new ValidationErrors().MaxLength("title", title, LectureNoteFile.MaxTitleLength);
        if (file.Length == 0 || file.Length > LectureNoteFile.MaxBytes)
            errors.Add("file", $"Dosya boş ya da çok büyük (en fazla {LectureNoteFile.MaxBytes / 1024 / 1024} MB).");
        if (errors.HasErrors)
            return errors.ToProblem();

        // Metin çıkarıcılar konumlanabilir akış ister; depo da uzak olabilir: dosya bellekte okunur (en fazla MaxBytes).
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var type = DocumentFiles.Detect(file.FileName, buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)));
        if (type is null)
            return ValidationErrors.Single("file", "Yalnız PDF, Word, PowerPoint, Excel ve resim (PNG, JPG) dosyası eklenebilir.");

        // Çıkarıcı akışı kapatabilir (zip): kendi görünümünü alır, tampon kaydetmek için açık kalır.
        var text = DocumentFiles.ExtractText(type.Extension, new MemoryStream(buffer.GetBuffer(), 0, (int)buffer.Length, writable: false));
        var fileName = Path.GetFileName(file.FileName);
        var entry = new LectureNoteFile
        {
            LectureNoteId = note.Id,
            Title = InputText.Clean(title) ?? Path.GetFileNameWithoutExtension(fileName),
            FileName = fileName.Length > 300 ? fileName[^300..] : fileName,
            ContentType = type.ContentType,
            SizeBytes = file.Length,
            PageCount = text.PageCount,
            Text = text.Text,
            SortOrder = (await db.LectureNoteFiles.Where(f => f.LectureNoteId == id).MaxAsync(f => (int?)f.SortOrder, ct) ?? -1) + 1,
        };
        entry.StorageKey = $"ders-notlari/{DateTime.UtcNow:yyyy/MM}/{entry.Id}{type.Extension}";

        buffer.Position = 0;
        await storage.SaveAsync(entry.StorageKey, buffer, ct);
        db.LectureNoteFiles.Add(entry);
        // Not de güncellenmiş sayılır (listede "son güncellenen" sırası).
        db.Entry(note).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
        var dto = ToFileDto(note, entry);
        return TypedResults.Created(dto.Url, dto);
    }

    private static async Task<Results<Ok<LectureNoteFileDto>, NotFound, ValidationProblem>> UpdateFileAsync(
        Guid id, Guid fileId, UpdateLectureNoteFileRequest request, AppDbContext db, CancellationToken ct)
    {
        var note = await db.LectureNotes.FindAsync([id], ct);
        var file = await db.LectureNoteFiles.FirstOrDefaultAsync(f => f.Id == fileId && f.LectureNoteId == id, ct);
        if (note is null || file is null)
            return TypedResults.NotFound();
        var errors = new ValidationErrors().Required("title", request.Title, LectureNoteFile.MaxTitleLength);
        if (errors.HasErrors)
            return errors.ToProblem();
        file.Title = InputText.CleanRequired(request.Title);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToFileDto(note, file));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> ReorderFilesAsync(
        Guid id, ReorderLectureNoteFilesRequest request, AppDbContext db, CancellationToken ct)
    {
        if (!await db.LectureNotes.AnyAsync(n => n.Id == id, ct))
            return TypedResults.NotFound();
        var files = await db.LectureNoteFiles.Where(f => f.LectureNoteId == id).ToListAsync(ct);
        if (request.FileIds is null || request.FileIds.Count != files.Count || request.FileIds.Distinct().Count() != files.Count
            || !files.All(f => request.FileIds.Contains(f.Id)))
            return ValidationErrors.Single("fileIds", "Sıra, ders notunun bütün belgelerini birer kez içermeli.");
        foreach (var file in files)
            file.SortOrder = request.FileIds.IndexOf(file.Id);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteFileAsync(
        Guid id, Guid fileId, AppDbContext db, IFileStorage storage, TimeProvider clock, CancellationToken ct)
    {
        var file = await db.LectureNoteFiles.FirstOrDefaultAsync(f => f.Id == fileId && f.LectureNoteId == id, ct);
        if (file is null)
            return TypedResults.NotFound();
        file.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(file.StorageKey, ct);
        return TypedResults.NoContent();
    }
}
