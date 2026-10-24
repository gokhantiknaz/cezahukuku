using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Content;

public sealed record NewsItemDto(Guid Id, string Title, string? Summary, string? Url, DateOnly PublishedOn, Visibility Visibility);

public sealed record SaveNewsItemRequest(string Title, string? Summary, string? Url, DateOnly PublishedOn, Visibility Visibility);

/// <summary>Ceza Gündemi: herkes okur (private haber yalnız girişte), yönetici girer.</summary>
public static class NewsEndpoints
{
    private const int MaxLimit = 100;

    public static void MapNewsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/news", ListAsync).WithTags("News").WithName("ListNews").AllowAnonymous();

        var admin = app.MapGroup("/api/admin/news").WithTags("News").RequireAuthorization(Policies.AdminOnly);
        admin.MapPost("/", CreateAsync).WithName("CreateNewsItem");
        admin.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateNewsItem");
        admin.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteNewsItem");
    }

    /// <summary>Haberler, yeniden eskiye. Yayın tarihi ileride olan haber o güne kadar görünmez.</summary>
    private static async Task<Ok<List<NewsItemDto>>> ListAsync(int? limit, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var includePrivate = principal.CanSeePrivate();
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var isAdmin = principal.IsInRole(Domain.Users.Roles.SuperAdmin);
        var items = await db.NewsItems.AsNoTracking()
            .Where(n => includePrivate || n.Visibility == Visibility.Public)
            .Where(n => isAdmin || n.PublishedOn <= today)
            .OrderByDescending(n => n.PublishedOn).ThenByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(limit ?? 20, 1, MaxLimit))
            .ToListAsync(ct);
        return TypedResults.Ok(items.Select(ToDto).ToList());
    }

    private static async Task<Results<Created<NewsItemDto>, ValidationProblem>> CreateAsync(SaveNewsItemRequest request, AppDbContext db, CancellationToken ct)
    {
        var item = new NewsItem();
        if (Apply(item, request) is { } problem)
            return problem;
        db.NewsItems.Add(item);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/admin/news/{item.Id}", ToDto(item));
    }

    private static async Task<Results<Ok<NewsItemDto>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, SaveNewsItemRequest request, AppDbContext db, CancellationToken ct)
    {
        var item = await db.NewsItems.FindAsync([id], ct);
        if (item is null)
            return TypedResults.NotFound();
        if (Apply(item, request) is { } problem)
            return problem;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(item));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var item = await db.NewsItems.FindAsync([id], ct);
        if (item is null)
            return TypedResults.NotFound();
        item.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static ValidationProblem? Apply(NewsItem item, SaveNewsItemRequest request)
    {
        var errors = new ValidationErrors()
            .Required("title", request.Title, NewsItem.MaxTitleLength)
            .MaxLength("summary", request.Summary, NewsItem.MaxSummaryLength);
        var url = InputText.Clean(request.Url);
        if (url is not null && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            errors.Add("url", "Bağlantı http:// ya da https:// ile başlayan bir adres olmalı.");
        if (errors.HasErrors)
            return errors.ToProblem();

        item.Title = InputText.CleanRequired(request.Title);
        item.Summary = InputText.Clean(request.Summary);
        item.Url = url;
        item.PublishedOn = request.PublishedOn;
        item.Visibility = request.Visibility;
        return null;
    }

    private static NewsItemDto ToDto(NewsItem n) => new(n.Id, n.Title, n.Summary, n.Url, n.PublishedOn, n.Visibility);
}
