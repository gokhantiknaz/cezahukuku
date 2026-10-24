using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Personal;

public sealed record FavoriteDto(LinkedArticleRef Article, DateTimeOffset CreatedAt);

/// <summary>Favori maddeler: yalnız sahibi görür. Ekleme ve kaldırma tekrarlanabilir (aynı sonuç).</summary>
public static class FavoriteEndpoints
{
    public static RouteGroupBuilder MapFavoriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/favorites", ListAsync).WithName("ListFavorites");
        group.MapPut("/favorites/{articleId:guid}", AddAsync).WithName("AddFavorite");
        group.MapDelete("/favorites/{articleId:guid}", RemoveAsync).WithName("RemoveFavorite");
        return group;
    }

    /// <summary>Favorilerim, son eklenen önce. Silinmiş maddeler listede çıkmaz.</summary>
    private static async Task<Ok<List<FavoriteDto>>> ListAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var userId = principal.UserId();
        var favorites = await db.Favorites.AsNoTracking().Where(f => f.OwnerId == userId).OrderByDescending(f => f.CreatedAt).ToListAsync(ct);
        var ids = favorites.Select(f => f.ArticleId).ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        var visible = await VisibleArticles.FilterAsync(db, articles, includePrivate: true);
        return TypedResults.Ok(favorites
            .Where(f => visible.ContainsKey(f.ArticleId))
            .Select(f => new FavoriteDto(VisibleArticles.ToRef(visible[f.ArticleId].Article, visible[f.ArticleId].Law), f.CreatedAt))
            .ToList());
    }

    private static async Task<Results<NoContent, NotFound>> AddAsync(Guid articleId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Articles.AnyAsync(a => a.Id == articleId, ct))
            return TypedResults.NotFound();
        var userId = principal.UserId();
        if (!await db.Favorites.AnyAsync(f => f.OwnerId == userId && f.ArticleId == articleId, ct))
        {
            db.Favorites.Add(new Favorite { OwnerId = userId, ArticleId = articleId });
            await db.SaveChangesAsync(ct);
        }
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemoveAsync(Guid articleId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var userId = principal.UserId();
        await db.Favorites.Where(f => f.OwnerId == userId && f.ArticleId == articleId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }
}
