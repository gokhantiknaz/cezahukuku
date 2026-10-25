using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Infrastructure.Attachments;
using CezaHukuku.Infrastructure.Files;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Legislation;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Personal;

/// <param name="Article">Bağlı olduğu madde (sesli not madde okunurken alınır); yoksa null.</param>
public sealed record AttachmentDto(
    Guid Id,
    AttachmentKind Kind,
    string Title,
    string FileName,
    string ContentType,
    long SizeBytes,
    AttachmentStatus Status,
    string? Error,
    int? PageCount,
    int? ScannedPageCount,
    double? DurationSeconds,
    bool HasTranscript,
    LinkedArticleRef? Article,
    DateTimeOffset CreatedAt);

/// <summary>Belgedeki madde atıfı (kütüphanedeki maddeye bağlanmış).</summary>
public sealed record DocumentCitationSpan(int Start, int Length, LinkedArticleRef Target);

/// <param name="Page">PDF sayfa numarası.</param>
/// <param name="StartSeconds">Sesli notta bölümün başladığı saniye.</param>
public sealed record AttachmentSegmentDto(Guid Id, int? Page, double? StartSeconds, string Text, List<DocumentCitationSpan> Citations);

/// <param name="TranscriptionEnabled">Sunucuda yazıya dökme servisi ayarlı mı (değilse sesli notun metnini kullanıcı yazar).</param>
public sealed record AttachmentDetail(AttachmentDto Attachment, List<AttachmentSegmentDto> Segments, bool TranscriptionEnabled);

/// <param name="Transcript">Yalnız sesli notta: kullanıcının yazdığı / düzelttiği metin (öncekinin yerine geçer).</param>
public sealed record UpdateAttachmentRequest(string? Title, string? Transcript);

/// <summary>Kişisel ekler: PDF belgeler ve sesli notlar. Yalnız sahibi görür (başkasının eki 404).</summary>
public static class AttachmentEndpoints
{
    private static readonly Dictionary<string, string> AudioTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/webm"] = ".webm",
        ["audio/ogg"] = ".ogg",
        ["audio/mp4"] = ".m4a",
        ["audio/x-m4a"] = ".m4a",
        ["audio/mpeg"] = ".mp3",
        ["audio/wav"] = ".wav",
        ["audio/x-wav"] = ".wav",
    };

    public static RouteGroupBuilder MapAttachmentEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/attachments", UploadAsync).WithName("UploadAttachment")
            // Çok parçalı form; SameSite=Lax oturum çerezi başka siteden gönderilen formda taşınmaz.
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(Attachment.MaxPdfBytes + 1024 * 1024));
        group.MapGet("/attachments", ListAsync).WithName("ListAttachments");
        group.MapGet("/attachments/{id:guid}", GetAsync).WithName("GetAttachment");
        group.MapGet("/attachments/{id:guid}/file", FileAsync).WithName("GetAttachmentFile");
        group.MapPut("/attachments/{id:guid}", UpdateAsync).WithName("UpdateAttachment");
        group.MapPost("/attachments/{id:guid}/reprocess", ReprocessAsync).WithName("ReprocessAttachment");
        group.MapDelete("/attachments/{id:guid}", DeleteAsync).WithName("DeleteAttachment");
        return group;
    }

    /// <summary>PDF ya da ses dosyası yükler; metni arka planda çıkarılır (durum: pending → ready).</summary>
    /// <param name="articleId">Bağlanacak madde (isteğe bağlı).</param>
    private static async Task<Results<Created<AttachmentDto>, NotFound, ValidationProblem>> UploadAsync(
        IFormFile file, [FromForm] string? title, [FromForm] Guid? articleId,
        ClaimsPrincipal principal, AppDbContext db, IFileStorage storage, AttachmentQueue queue, CancellationToken ct)
    {
        if (articleId is { } id && !await db.Articles.AnyAsync(a => a.Id == id, ct))
            return TypedResults.NotFound();

        var kind = await DetectKindAsync(file);
        if (kind is null)
            return ValidationErrors.Single("file", "Yalnız PDF ve ses dosyası (webm, ogg, m4a, mp3, wav) yüklenebilir.");
        var limit = kind == AttachmentKind.Pdf ? Attachment.MaxPdfBytes : Attachment.MaxAudioBytes;
        if (file.Length == 0 || file.Length > limit)
            return ValidationErrors.Single("file", $"Dosya boş ya da çok büyük (en fazla {limit / 1024 / 1024} MB).");

        var errors = new ValidationErrors().MaxLength("title", title, Attachment.MaxTitleLength);
        if (errors.HasErrors)
            return errors.ToProblem();

        var attachment = new Attachment
        {
            OwnerId = principal.UserId(),
            ArticleId = articleId,
            Kind = kind.Value,
            FileName = Path.GetFileName(file.FileName),
            ContentType = kind == AttachmentKind.Pdf ? "application/pdf" : BaseType(file.ContentType),
            SizeBytes = file.Length,
            Status = AttachmentStatus.Pending,
        };
        attachment.Title = InputText.Clean(title)
            ?? (kind == AttachmentKind.Pdf ? Path.GetFileNameWithoutExtension(attachment.FileName) : $"Sesli not {DateTime.Now:dd.MM.yyyy HH:mm}");
        var extension = kind == AttachmentKind.Pdf ? ".pdf" : AudioTypes[attachment.ContentType];
        attachment.StorageKey = $"ekler/{DateTime.UtcNow:yyyy/MM}/{attachment.Id}{extension}";

        await using (var content = file.OpenReadStream())
            await storage.SaveAsync(attachment.StorageKey, content, ct);
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync(ct);
        queue.Notify();
        return TypedResults.Created($"/api/me/attachments/{attachment.Id}", await ToDtoAsync(db, attachment, ct));
    }

    /// <summary>Eklerim, yeniden eskiye.</summary>
    /// <param name="articleId">Yalnız bu maddeye bağlı olanlar.</param>
    /// <param name="kind">Yalnız bu tür: pdf, audio.</param>
    private static async Task<Ok<List<AttachmentDto>>> ListAsync(
        Guid? articleId, string? kind, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var userId = principal.UserId();
        AttachmentKind? kindFilter = Enum.TryParse<AttachmentKind>(kind, ignoreCase: true, out var parsed) ? parsed : null;
        var attachments = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerId == userId)
            .Where(a => articleId == null || a.ArticleId == articleId)
            .Where(a => kindFilter == null || a.Kind == kindFilter)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        var articles = await ArticleRefsAsync(db, attachments, ct);
        return TypedResults.Ok(attachments.Select(a => ToDto(a, a.ArticleId is { } id ? articles.GetValueOrDefault(id) : null)).ToList());
    }

    private static async Task<Results<Ok<AttachmentDetail>, NotFound>> GetAsync(
        Guid id, ClaimsPrincipal principal, AppDbContext db, ITranscriber transcriber, CancellationToken ct)
    {
        var attachment = await FindOwnAsync(db, id, principal, ct);
        if (attachment is null)
            return TypedResults.NotFound();

        var segments = await db.AttachmentSegments.AsNoTracking()
            .Where(s => s.AttachmentId == id).OrderBy(s => s.Index).ToListAsync(ct);
        var citations = await ResolveCitationsAsync(db, segments, ct);
        return TypedResults.Ok(new AttachmentDetail(
            await ToDtoAsync(db, attachment, ct),
            [.. segments.Select(s => new AttachmentSegmentDto(s.Id, s.Page, s.StartSeconds, s.Text, citations.GetValueOrDefault(s.Id, [])))],
            transcriber.IsEnabled));
    }

    /// <summary>Dosyanın kendisi (PDF tarayıcıda açılır; ses parça parça okunabilir, ileri sarılabilir).</summary>
    /// <param name="download">true: indirilecek dosya olarak (özgün adıyla).</param>
    private static async Task<Results<FileStreamHttpResult, NotFound>> FileAsync(
        Guid id, bool? download, ClaimsPrincipal principal, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var attachment = await FindOwnAsync(db, id, principal, ct);
        if (attachment is null || await storage.OpenReadAsync(attachment.StorageKey, ct) is not { } stream)
            return TypedResults.NotFound();
        return TypedResults.Stream(stream, attachment.ContentType,
            fileDownloadName: download == true ? attachment.FileName : null, enableRangeProcessing: true);
    }

    private static async Task<Results<Ok<AttachmentDto>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, UpdateAttachmentRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var attachment = await FindOwnAsync(db, id, principal, ct, tracking: true);
        if (attachment is null)
            return TypedResults.NotFound();

        var errors = new ValidationErrors().MaxLength("title", request.Title, Attachment.MaxTitleLength);
        if (request.Transcript is { Length: > 100_000 })
            errors.Add("transcript", "Metin çok uzun.");
        if (request.Transcript is not null && attachment.Kind != AttachmentKind.Audio)
            errors.Add("transcript", "Metin yalnız sesli notta düzenlenir.");
        if (errors.HasErrors)
            return errors.ToProblem();

        if (InputText.Clean(request.Title) is { } title)
            attachment.Title = title;
        if (request.Transcript is not null)
        {
            // Kullanıcının yazdığı metin tek bölüm olarak öncekinin (servisin döktüğünün) yerine geçer.
            var text = InputText.Clean(request.Transcript.Replace("\r\n", "\n"));
            await db.AttachmentSegments.Where(s => s.AttachmentId == id).ExecuteDeleteAsync(ct);
            if (text is not null)
                db.AttachmentSegments.Add(new AttachmentSegment { AttachmentId = id, Index = 0, StartSeconds = 0, Text = text });
            attachment.HasTranscript = text is not null;
            attachment.Status = AttachmentStatus.Ready;
        }
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ToDtoAsync(db, attachment, ct));
    }

    /// <summary>Eki yeniden işler (örn. yazıya dökme servisi sonradan ayarlandıysa).</summary>
    private static async Task<Results<Ok<AttachmentDto>, NotFound>> ReprocessAsync(
        Guid id, ClaimsPrincipal principal, AppDbContext db, AttachmentQueue queue, CancellationToken ct)
    {
        var attachment = await FindOwnAsync(db, id, principal, ct, tracking: true);
        if (attachment is null)
            return TypedResults.NotFound();
        attachment.Status = AttachmentStatus.Pending;
        attachment.Error = null;
        await db.SaveChangesAsync(ct);
        queue.Notify();
        return TypedResults.Ok(await ToDtoAsync(db, attachment, ct));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, ClaimsPrincipal principal, AppDbContext db, IFileStorage storage, TimeProvider clock, CancellationToken ct)
    {
        var attachment = await FindOwnAsync(db, id, principal, ct, tracking: true);
        if (attachment is null)
            return TypedResults.NotFound();
        attachment.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        // Kayıt iz için kalır; kişisel dosyanın kendisi silinir.
        await storage.DeleteAsync(attachment.StorageKey, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Attachment?> FindOwnAsync(
        AppDbContext db, Guid id, ClaimsPrincipal principal, CancellationToken ct, bool tracking = false)
    {
        var userId = principal.UserId();
        var query = tracking ? db.Attachments : db.Attachments.AsNoTracking();
        return await query.FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == userId, ct);
    }

    /// <summary>Türü içerikten belirler: PDF "%PDF-" ile başlar; ses tarayıcının bildirdiği türden.</summary>
    private static async Task<AttachmentKind?> DetectKindAsync(IFormFile file)
    {
        var header = new byte[5];
        await using (var stream = file.OpenReadStream())
            _ = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        if (header.AsSpan().SequenceEqual("%PDF-"u8))
            return AttachmentKind.Pdf;
        return AudioTypes.ContainsKey(BaseType(file.ContentType)) ? AttachmentKind.Audio : null;
    }

    // "audio/webm;codecs=opus" → "audio/webm"
    private static string BaseType(string? contentType) => (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();

    private static async Task<AttachmentDto> ToDtoAsync(AppDbContext db, Attachment attachment, CancellationToken ct)
    {
        var refs = await ArticleRefsAsync(db, [attachment], ct);
        return ToDto(attachment, attachment.ArticleId is { } id ? refs.GetValueOrDefault(id) : null);
    }

    private static AttachmentDto ToDto(Attachment a, LinkedArticleRef? article) =>
        new(a.Id, a.Kind, a.Title, a.FileName, a.ContentType, a.SizeBytes, a.Status, a.Error, a.PageCount, a.ScannedPageCount,
            a.DurationSeconds, a.HasTranscript, article, a.CreatedAt);

    private static async Task<Dictionary<Guid, LinkedArticleRef>> ArticleRefsAsync(
        AppDbContext db, IReadOnlyCollection<Attachment> attachments, CancellationToken ct)
    {
        var ids = attachments.Where(a => a.ArticleId != null).Select(a => a.ArticleId!.Value).Distinct().ToList();
        if (ids.Count == 0)
            return [];
        var articles = await db.Articles.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        var visible = await VisibleArticles.FilterAsync(db, articles, includePrivate: true);
        return visible.ToDictionary(v => v.Key, v => VisibleArticles.ToRef(v.Value.Article, v.Value.Law));
    }

    /// <summary>
    /// Belge metnindeki atıfları kütüphanedeki maddelere bağlar (<see cref="CitedLaws"/>); madde kütüphanede yoksa
    /// atıf düz metin kalır.
    /// </summary>
    private static async Task<Dictionary<Guid, List<DocumentCitationSpan>>> ResolveCitationsAsync(
        AppDbContext db, List<AttachmentSegment> segments, CancellationToken ct)
    {
        var found = segments.ToDictionary(s => s.Id, s => DocumentCitationFinder.Find(s.Text));
        if (found.Values.All(c => c.Count == 0))
            return [];

        var laws = await CitedLaws.LoadAsync(db, includePrivate: true, ct);
        Guid? LawOf(DocumentCitation c) => laws.Find(c)?.Id;

        var wanted = found.Values.SelectMany(c => c)
            .Select(c => (Law: LawOf(c), Slug: Slugs.ForArticle(c.ArticleNumber)))
            .Where(x => x.Law is not null)
            .ToList();
        var lawIds = wanted.Select(w => w.Law!.Value).Distinct().ToList();
        var slugs = wanted.Select(w => w.Slug).Distinct().ToList();
        var candidates = await db.Articles.AsNoTracking()
            .Where(a => lawIds.Contains(a.LawId) && slugs.Contains(a.Slug)).ToListAsync(ct);
        var visible = await VisibleArticles.FilterAsync(db, candidates, includePrivate: true);
        var targets = visible.Values.ToDictionary(v => (v.Law.Id, v.Article.Slug), v => VisibleArticles.ToRef(v.Article, v.Law));

        return found.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .Select(c => LawOf(c) is { } lawId && targets.TryGetValue((lawId, Slugs.ForArticle(c.ArticleNumber)), out var target)
                    ? new DocumentCitationSpan(c.Start, c.Length, target)
                    : null)
                .OfType<DocumentCitationSpan>()
                .ToList());
    }
}
