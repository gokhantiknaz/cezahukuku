using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Library;

/// <summary>
/// Okuyucu uç noktaları. Anonim istek yalnız public içeriği görür; private içerik yokmuş gibi 404 döner
/// (varlığı bile sızmasın). Giriş yapmış kullanıcı her şeyi görür.
/// </summary>
public static class LibraryEndpoints
{
    public static RouteGroupBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/library").WithTags("Library").AllowAnonymous();

        group.MapGet("/", GetLibraryAsync).WithName("GetLibrary");
        group.MapGet("/laws/{lawSlug}", GetLawAsync).WithName("GetLaw");
        group.MapGet("/laws/{lawSlug}/articles/{articleSlug}", GetArticleAsync).WithName("GetArticle");
        group.MapGet("/laws/{lawSlug}/articles/{articleSlug}/history", GetArticleHistoryAsync).WithName("GetArticleHistory");
        group.MapGet("/laws/{lawSlug}/notes", GetLawNotesAsync).WithName("GetLawNotes");

        return group;
    }

    /// <summary>Maddenin tüm sürümleri metinleriyle, en yeni önce (Değişiklikler sekmesi ve zaman tüneli).</summary>
    private static async Task<Results<Ok<List<ArticleVersionDetail>>, NotFound>> GetArticleHistoryAsync(
        string lawSlug, string articleSlug, ClaimsPrincipal principal, AppDbContext db)
    {
        var loaded = await LoadOutlineAsync(db, lawSlug, principal.CanSeePrivate());
        if (loaded is not ({ }, { } outline))
            return TypedResults.NotFound();

        var slug = articleSlug.ToLowerInvariant();
        var item = outline.ReadingOrder.FirstOrDefault(a => a.Article.Slug == slug);
        if (item is null)
            return TypedResults.NotFound();

        var versions = await db.ArticleVersions.AsNoTracking()
            .Where(v => v.ArticleId == item.Article.Id)
            .ToListAsync();
        return TypedResults.Ok(versions
            .OrderByDescending(v => v.ValidFrom ?? DateOnly.MinValue)
            .Select(v => v.ToDetail())
            .ToList());
    }

    private static async Task<Ok<LibraryResponse>> GetLibraryAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var includePrivate = principal.CanSeePrivate();
        var groups = await db.LawGroups.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Title).ToListAsync();
        var laws = await db.Laws.AsNoTracking()
            .Where(l => includePrivate || l.Visibility == Visibility.Public)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .ToListAsync();

        var lawsByGroup = laws.ToLookup(l => l.GroupId);
        var response = new LibraryResponse(
            [.. groups.Select(g => new LawGroupWithLaws(g.Id, g.Slug, g.Title, g.SortOrder,
                [.. lawsByGroup[g.Id].Select(l => l.ToSummary())]))]);
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<LawTreeResponse>, NotFound>> GetLawAsync(
        string lawSlug, ClaimsPrincipal principal, AppDbContext db)
    {
        var loaded = await LoadOutlineAsync(db, lawSlug, principal.CanSeePrivate());
        if (loaded is not ({ } law, { } outline))
            return TypedResults.NotFound();

        var includePrivate = principal.CanSeePrivate();
        var noteKinds = await db.EditorialNotes.AsNoTracking()
            .Where(n => n.LawId == law.Id && n.ArticleId == null && (includePrivate || n.Visibility == Visibility.Public))
            .Select(n => n.Kind)
            .Distinct()
            .ToListAsync();

        return TypedResults.Ok(new LawTreeResponse(
            law.ToSummary(), [.. outline.Roots.Select(n => n.ToTree())], [.. outline.RootArticles.Select(a => a.ToSummary())],
            [.. noteKinds.Order()]));
    }

    /// <param name="asOf">Bu tarihte yürürlükte olan metni getir (örn. suç tarihi). Verilmezse güncel metin.</param>
    private static async Task<Results<Ok<ArticleDetailResponse>, NotFound>> GetArticleAsync(
        string lawSlug, string articleSlug, DateOnly? asOf, ClaimsPrincipal principal, AppDbContext db)
    {
        var loaded = await LoadOutlineAsync(db, lawSlug, principal.CanSeePrivate());
        if (loaded is not ({ } law, { } outline))
            return TypedResults.NotFound();

        var slug = articleSlug.ToLowerInvariant();
        var item = outline.ReadingOrder.FirstOrDefault(a => a.Article.Slug == slug);
        if (item is null)
            return TypedResults.NotFound();

        var versions = await db.ArticleVersions.AsNoTracking()
            .Where(v => v.ArticleId == item.Article.Id)
            .OrderByDescending(v => v.ValidFrom)
            .ToListAsync();
        var notes = await VisibleNotesAsync(db, law.Id, item.Article.Id, item.EffectiveVisibility, principal.CanSeePrivate());

        var response = new ArticleDetailResponse(
            item.ToSummary(),
            law.ToSummary(),
            [.. outline.PathOf(item.Article).Select(n => n.ToRef())],
            ArticleVersion.Select(versions, asOf)?.ToDetail(),
            [.. versions.Select(v => v.ToSummary())],
            outline.Previous(item.Article.Id)?.ToRef(),
            outline.Next(item.Article.Id)?.ToRef(),
            notes);
        return TypedResults.Ok(response);
    }

    /// <summary>Kanun düzeyindeki notlar (Genel Gerekçe, Ders Notları).</summary>
    /// <param name="kind">Verilirse yalnız o tür: "genelGerekce", "dersNotu" (büyük/küçük harf fark etmez).</param>
    private static async Task<Results<Ok<List<EditorialNoteDto>>, NotFound>> GetLawNotesAsync(
        string lawSlug, string? kind, ClaimsPrincipal principal, AppDbContext db)
    {
        // Sorgu dizgisindeki enum harfe duyarlı çözülür (JSON'daki camelCase dönüştürücüsü burada geçmez).
        EditorialNoteKind? kindFilter = Enum.TryParse<EditorialNoteKind>(kind, ignoreCase: true, out var parsed) ? parsed : null;
        var includePrivate = principal.CanSeePrivate();
        var slug = lawSlug.ToLowerInvariant();
        var law = await db.Laws.AsNoTracking().FirstOrDefaultAsync(l => l.Slug == slug);
        if (law is null || (!includePrivate && law.Visibility == Visibility.Private))
            return TypedResults.NotFound();

        var notes = await VisibleNotesAsync(db, law.Id, null, law.Visibility, includePrivate);
        return TypedResults.Ok(kindFilter is null ? notes : [.. notes.Where(n => n.Kind == kindFilter)]);
    }

    /// <summary>
    /// Maddenin (ya da <paramref name="articleId"/> null ise kanunun) notları; geçerli görünürlük bağlı olduğu öğenin
    /// görünürlüğüyle daralır.
    /// </summary>
    internal static async Task<List<EditorialNoteDto>> VisibleNotesAsync(
        AppDbContext db, Guid lawId, Guid? articleId, Visibility parentVisibility, bool includePrivate)
    {
        var notes = await db.EditorialNotes.AsNoTracking()
            .Where(n => n.LawId == lawId && n.ArticleId == articleId)
            .OrderBy(n => n.Kind).ThenBy(n => n.SortOrder).ThenBy(n => n.CreatedAt)
            .ToListAsync();
        return
        [
            .. notes
                .Select(n => (Note: n, Effective: parentVisibility.Narrow(n.Visibility)))
                .Where(x => includePrivate || x.Effective == Visibility.Public)
                .Select(x => x.Note.ToDto(x.Effective)),
        ];
    }

    /// <summary>Kanunu ve görünür ağacını yükler; kanun yoksa ya da kullanıcıya kapalıysa null.</summary>
    internal static async Task<(Law Law, LawOutline Outline)?> LoadOutlineAsync(
        AppDbContext db, string lawSlug, bool includePrivate)
    {
        var slug = lawSlug.ToLowerInvariant();
        var law = await db.Laws.AsNoTracking().FirstOrDefaultAsync(l => l.Slug == slug);
        if (law is null || (!includePrivate && law.Visibility == Visibility.Private))
            return null;

        var nodes = await db.LawNodes.AsNoTracking().Where(n => n.LawId == law.Id).ToListAsync();
        var articles = await db.Articles.AsNoTracking().Where(a => a.LawId == law.Id).ToListAsync();
        return (law, LawOutline.Build(law, nodes, articles, includePrivate));
    }
}
