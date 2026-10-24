using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Personal;

/// <param name="Segments">Vurgunun gösterilen sürümdeki parçaları; metinde bulunamayan parçalar yer almaz.</param>
/// <param name="Quote">Vurgulanan metin (parçalar " … " ile birleşik); metinde bulunamasa da gösterilebilsin.</param>
/// <param name="IsStale">Parçalardan en az biri gösterilen sürümde bulunamadı (metin değişmiş: "eski sürüme ait").</param>
public sealed record HighlightDto(
    Guid Id, HighlightColor Color, string Quote, IReadOnlyList<TextPosition> Segments, bool IsStale, DateTimeOffset CreatedAt);

/// <param name="ArticleVersionId">Seçimin yapıldığı (ekranda gösterilen) sürüm; konumlar bu sürümün metnine göre.</param>
/// <param name="Segments">Seçimin fıkra/bent başına parçaları (fıkra ya da bent metnindeki başlangıç ve uzunluk).</param>
public sealed record CreateHighlightRequest(Guid ArticleVersionId, HighlightColor Color, IReadOnlyList<TextPosition> Segments);

public sealed record UpdateHighlightRequest(HighlightColor Color);

/// <summary>
/// Kişisel vurgular: yalnız sahibi görür ve değiştirir (başkasının vurgusu 404). reader rolü kişisel alan
/// oluşturamaz (403).
/// </summary>
public static class HighlightEndpoints
{
    private const int MaxSegments = 100;

    public static RouteGroupBuilder MapHighlightEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").WithTags("Personal").RequireAuthorization(Policies.PersonalLayer);

        group.MapGet("/articles/{articleId:guid}/highlights", ListAsync).WithName("ListHighlights");
        group.MapPost("/highlights", CreateAsync).WithName("CreateHighlight");
        group.MapPut("/highlights/{id:guid}", UpdateAsync).WithName("UpdateHighlight");
        group.MapDelete("/highlights/{id:guid}", DeleteAsync).WithName("DeleteHighlight");

        return group;
    }

    /// <summary>Maddedeki vurgularım, verilen sürümün (verilmezse yürürlükteki metnin) konumlarıyla.</summary>
    private static async Task<Results<Ok<List<HighlightDto>>, NotFound>> ListAsync(
        Guid articleId, Guid? versionId, ClaimsPrincipal principal, AppDbContext db)
    {
        if (!await db.Articles.AnyAsync(a => a.Id == articleId))
            return TypedResults.NotFound();

        var versions = await db.ArticleVersions.AsNoTracking().Where(v => v.ArticleId == articleId).ToListAsync();
        var shown = versionId is { } id ? versions.FirstOrDefault(v => v.Id == id) : ArticleVersion.Select(versions, null);
        if (versionId is not null && shown is null)
            return TypedResults.NotFound();

        var userId = principal.UserId();
        var highlights = await db.Highlights.AsNoTracking()
            .Where(h => h.OwnerId == userId && h.ArticleId == articleId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync();
        return TypedResults.Ok(highlights.Select(h => ToDto(h, shown?.Paragraphs ?? [])).ToList());
    }

    private static async Task<Results<Created<HighlightDto>, NotFound, ValidationProblem>> CreateAsync(
        CreateHighlightRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var version = await db.ArticleVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.ArticleVersionId);
        if (version is null || !await db.Articles.AnyAsync(a => a.Id == version.ArticleId))
            return TypedResults.NotFound();

        var errors = new ValidationErrors();
        if (!Enum.IsDefined(request.Color))
            errors.Add("color", "Geçersiz renk.");
        if (request.Segments is not { Count: > 0 and <= MaxSegments })
            errors.Add("segments", "Vurgulanacak metin seçilmedi.");

        var anchors = (request.Segments ?? []).Select(s => TextAnchoring.Create(version.Paragraphs, s)).ToList();
        if (anchors.Any(a => a is null))
            errors.Add("segments", "Seçim madde metniyle uyuşmuyor; sayfayı yenileyip tekrar deneyin.");
        if (errors.HasErrors)
            return errors.ToProblem();

        var highlight = new Highlight
        {
            OwnerId = principal.UserId(),
            ArticleId = version.ArticleId,
            ArticleVersionId = version.Id,
            Color = request.Color,
            Segments = [.. anchors.Select(a => a!)],
        };
        db.Highlights.Add(highlight);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/me/highlights/{highlight.Id}", ToDto(highlight, version.Paragraphs));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, UpdateHighlightRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.UserId();
        var highlight = await db.Highlights.FirstOrDefaultAsync(h => h.Id == id && h.OwnerId == userId);
        if (highlight is null)
            return TypedResults.NotFound();
        if (!Enum.IsDefined(request.Color))
            return ValidationErrors.Single("color", "Geçersiz renk.");

        highlight.Color = request.Color;
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var userId = principal.UserId();
        var highlight = await db.Highlights.FirstOrDefaultAsync(h => h.Id == id && h.OwnerId == userId);
        if (highlight is null)
            return TypedResults.NotFound();

        highlight.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static HighlightDto ToDto(Highlight highlight, IReadOnlyList<Paragraph> paragraphs)
    {
        var located = highlight.Segments
            .Select(s => TextAnchoring.Locate(s, paragraphs))
            .OfType<TextPosition>()
            .ToList();
        return new HighlightDto(
            highlight.Id,
            highlight.Color,
            string.Join(" … ", highlight.Segments.Select(s => s.Quote)),
            located,
            located.Count < highlight.Segments.Count,
            highlight.CreatedAt);
    }
}
