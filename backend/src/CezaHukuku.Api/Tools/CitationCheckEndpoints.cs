using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CezaHukuku.Api.Tools;

/// <summary>
/// Atıf ve künye denetimi: yapıştırılan dilekçe, karar ya da makale metnindeki madde atıflarını ve karar künyelerini
/// kütüphaneye göre denetler (yapay zekâ kullanılmaz). Herkese açıktır; anonim kullanıcı yalnız herkese açık içerikle
/// karşılaştırılır. Metin saklanmaz.
/// </summary>
public static class CitationCheckEndpoints
{
    public const int MaxTextLength = 300_000;
    private const long MaxFileBytes = 20 * 1024 * 1024;

    public static void MapCitationCheckEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tools").WithTags("Tools");
        group.MapPost("/citation-check", CheckAsync).WithName("CheckCitations").AllowAnonymous();
        group.MapPost("/extract-text", ExtractTextAsync).WithName("ExtractText")
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxFileBytes + 1024 * 1024));
    }

    private static async Task<Results<Ok<CitationCheckResult>, ValidationProblem>> CheckAsync(
        CitationCheckRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var text = request.Text ?? "";
        if (string.IsNullOrWhiteSpace(text))
            return ValidationErrors.Single("text", "Denetlenecek metni yapıştırın.");
        if (text.Length > MaxTextLength)
            return ValidationErrors.Single("text", $"Metin en fazla {MaxTextLength:N0} karakter olabilir.");

        var includePrivate = principal.CanSeePrivate();
        var decisions = await DecisionCitationCheck.CheckAsync(db, text, DateOnly.FromDateTime(DateTime.Now), includePrivate, ct);
        var articles = await ArticleCitationCheck.CheckAsync(db, text, request.ReferenceDate, includePrivate, ct);
        // Künyenin içindeki kısaltma + sayı ("YCGK 2017/1-5") madde atıfı değildir.
        articles.RemoveAll(a => decisions.Any(d => a.Start < d.Start + d.Length && d.Start < a.Start + a.Length));
        return TypedResults.Ok(new CitationCheckResult(articles, decisions));
    }

    /// <summary>Denetlenecek metni Word (.docx), PDF ya da metin dosyasından alır; dosya saklanmaz.</summary>
    private static async Task<Results<Ok<ExtractedText>, ValidationProblem>> ExtractTextAsync(IFormFile file, CancellationToken ct) =>
        await TextFiles.ExtractAsync(file, MaxFileBytes, "\n\n", ct) switch
        {
            { Result: Ok<ExtractedFileText> ok } => TypedResults.Ok(new ExtractedText(ok.Value!.Text)),
            { Result: ValidationProblem problem } => problem,
            _ => throw new InvalidOperationException(),
        };
}
