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

        return group;
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

        return TypedResults.Ok(new LawTreeResponse(
            law.ToSummary(), [.. outline.Roots.Select(n => n.ToTree())], [.. outline.RootArticles.Select(a => a.ToSummary())]));
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

        var response = new ArticleDetailResponse(
            item.ToSummary(),
            law.ToSummary(),
            [.. outline.PathOf(item.Article).Select(n => n.ToRef())],
            ArticleVersion.Select(versions, asOf)?.ToDetail(),
            [.. versions.Select(v => v.ToSummary())],
            outline.Previous(item.Article.Id)?.ToRef(),
            outline.Next(item.Article.Id)?.ToRef());
        return TypedResults.Ok(response);
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
