using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Content;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Content;

public sealed record LawGroupRef(Guid Id, string Slug, string Title);

public sealed record LectureNoteLawRef(Guid Id, string Slug, string Label, string Name);

/// <summary>Ders notunun bağlı olduğu yer; hepsi boşsa genel ders notu. Okuyanın göremediği kanun/madde gösterilmez.</summary>
public sealed record LectureNoteScope(LawGroupRef? Group, LectureNoteLawRef? Law, LinkedArticleRef? Article);

/// <param name="Excerpt">Metnin başı (liste için).</param>
public sealed record LectureNoteSummary(
    Guid Id, string Slug, string Title, string Excerpt, LectureNoteScope Scope, Visibility Visibility, DateTimeOffset UpdatedAt);

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
    Guid? ArticleId);

/// <summary>Maddenin altında gösterilen ders notu bağlantısı.</summary>
public sealed record LectureNoteRef(string Slug, string Title);

/// <param name="LawGroupId">İsteğe bağlı: kanun grubu (Temel Ceza Kanunları…).</param>
/// <param name="LawId">İsteğe bağlı: kanun; verilirse grubu kanunun grubu olur.</param>
/// <param name="ArticleId">İsteğe bağlı: madde; verilirse kanunu ve grubu maddeninki olur.</param>
public sealed record SaveLectureNoteRequest(
    string Title, string? Html, Visibility Visibility, Guid? LawGroupId, Guid? LawId, Guid? ArticleId);

/// <summary>
/// Ders Notları: kanundan bağımsız konu anlatımları. Herkes okur (private not yalnız girişte), yönetici girer.
/// İsteğe bağlı olarak kanun grubuna, kanuna ya da maddeye bağlanır.
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

        var admin = app.MapGroup("/api/admin/lecture-notes").WithTags("LectureNotes").RequireAuthorization(Policies.AdminOnly);
        admin.MapPost("/", CreateAsync).WithName("CreateLectureNote");
        admin.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateLectureNote");
        admin.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteLectureNote");
    }

    /// <summary>Ders notları, son güncellenen önce. Süzme (kapsam, metin) istemcide yapılır; notlar az sayıdadır.</summary>
    private static async Task<Ok<List<LectureNoteSummary>>> ListAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var includePrivate = principal.CanSeePrivate();
        var notes = await db.LectureNotes.AsNoTracking()
            .Where(n => includePrivate || n.Visibility == Visibility.Public)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(ct);
        var scopes = await ScopesAsync(db, notes, includePrivate, ct);
        return TypedResults.Ok(notes
            .Select(n => new LectureNoteSummary(n.Id, n.Slug, n.Title, Excerpt(n.PlainText), scopes[n.Id], n.Visibility, n.UpdatedAt))
            .ToList());
    }

    private static async Task<Results<Ok<LectureNoteDetail>, NotFound>> GetAsync(
        string slug, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var includePrivate = principal.CanSeePrivate();
        var note = await db.LectureNotes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Slug == slug.ToLowerInvariant() && (includePrivate || n.Visibility == Visibility.Public), ct);
        if (note is null)
            return TypedResults.NotFound();
        var scopes = await ScopesAsync(db, [note], includePrivate, ct);
        return TypedResults.Ok(ToDetail(note, scopes[note.Id]));
    }

    private static async Task<Results<Created<LectureNoteDetail>, ValidationProblem>> CreateAsync(
        SaveLectureNoteRequest request, AppDbContext db, CancellationToken ct)
    {
        var note = new LectureNote();
        if (await ApplyAsync(note, request, db, ct) is { } problem)
            return problem;
        note.Slug = await UniqueSlugAsync(db, note.Title, ct);
        db.LectureNotes.Add(note);
        await db.SaveChangesAsync(ct);
        var scopes = await ScopesAsync(db, [note], includePrivate: true, ct);
        return TypedResults.Created($"/api/lecture-notes/{note.Slug}", ToDetail(note, scopes[note.Id]));
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
        var scopes = await ScopesAsync(db, [note], includePrivate: true, ct);
        return TypedResults.Ok(ToDetail(note, scopes[note.Id]));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var note = await db.LectureNotes.FindAsync([id], ct);
        if (note is null)
            return TypedResults.NotFound();
        note.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Maddeye bağlı, okuyanın görebileceği ders notları (madde ekranında).</summary>
    public static async Task<List<LectureNoteRef>> ForArticleAsync(AppDbContext db, Guid articleId, bool includePrivate) =>
        await db.LectureNotes.AsNoTracking()
            .Where(n => n.ArticleId == articleId && (includePrivate || n.Visibility == Visibility.Public))
            .OrderBy(n => n.Title)
            .Select(n => new LectureNoteRef(n.Slug, n.Title))
            .ToListAsync();

    private static async Task<ValidationProblem?> ApplyAsync(
        LectureNote note, SaveLectureNoteRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = new ValidationErrors().Required("title", request.Title, LectureNote.MaxTitleLength);
        if (request.Html is { Length: > MaxHtmlLength })
            errors.Add("html", "Metin çok uzun.");
        var html = RichText.Sanitize(request.Html);
        if (html.Length == 0)
            errors.Add("html", "Ders notunun metni boş olamaz.");

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

    /// <summary>Başlıktan adres: "Kast ve taksir" → "kast-ve-taksir"; varsa "-2", "-3"…</summary>
    private static async Task<string> UniqueSlugAsync(AppDbContext db, string title, CancellationToken ct)
    {
        var slug = Slugs.FromText(title);
        if (slug.Length > 100)
            slug = slug[..100].TrimEnd('-');
        if (slug.Length == 0)
            slug = "ders-notu";
        else if (slug == "yeni")
            slug = "yeni-ders-notu"; // /ders-notlari/yeni yeni not formudur
        var taken = await db.LectureNotes.IgnoreQueryFilters()
            .Where(n => n.DeletedAt == null && (n.Slug == slug || n.Slug.StartsWith(slug + "-")))
            .Select(n => n.Slug)
            .ToListAsync(ct);
        var candidate = slug;
        for (var i = 2; taken.Contains(candidate); i++)
            candidate = $"{slug}-{i}";
        return candidate;
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

    private static LectureNoteDetail ToDetail(LectureNote n, LectureNoteScope scope) =>
        new(n.Id, n.Slug, n.Title, n.Html, scope, n.Visibility, n.CreatedAt, n.UpdatedAt, n.LawGroupId, n.LawId, n.ArticleId);
}
