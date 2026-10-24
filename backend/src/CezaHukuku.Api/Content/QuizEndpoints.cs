using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Content;

/// <param name="Article">Sorunun bağlı olduğu madde (yanıttan sonra "maddeye git").</param>
public sealed record QuizQuestionDto(
    Guid Id,
    Guid LawId,
    string LawLabel,
    string Text,
    List<string> Options,
    int CorrectIndex,
    string? Explanation,
    LinkedArticleRef? Article,
    Visibility Visibility);

public sealed record SaveQuizQuestionRequest(
    Guid LawId, Guid? ArticleId, string Text, List<string> Options, int CorrectIndex, string? Explanation, Visibility Visibility);

/// <summary>
/// Kendini Test Et: rastgele sorular (kanuna göre süzülebilir). Kendi kendini sınama aracı olduğundan doğru yanıt
/// soruyla birlikte gelir, değerlendirme tarayıcıda yapılır. Soruları yönetici girer.
/// </summary>
public static class QuizEndpoints
{
    private const int MaxCount = 50;

    public static void MapQuizEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/quiz", RandomAsync).WithTags("Quiz").WithName("GetQuiz").AllowAnonymous();

        var admin = app.MapGroup("/api/admin/quiz").WithTags("Quiz").RequireAuthorization(Policies.AdminOnly);
        admin.MapGet("/", ListAsync).WithName("ListQuizQuestions");
        admin.MapPost("/", CreateAsync).WithName("CreateQuizQuestion");
        admin.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateQuizQuestion");
        admin.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteQuizQuestion");
    }

    /// <param name="law">Yalnız bu kanunun soruları (adres adı: "tck").</param>
    /// <param name="count">Soru sayısı (varsayılan 10).</param>
    private static async Task<Ok<List<QuizQuestionDto>>> RandomAsync(
        string? law, int? count, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var includePrivate = principal.CanSeePrivate();
        var query = db.QuizQuestions.AsNoTracking().Where(q => includePrivate || q.Visibility == Visibility.Public);
        if (!string.IsNullOrWhiteSpace(law))
        {
            var slug = law.ToLowerInvariant();
            query = query.Where(q => db.Laws.Any(l => l.Id == q.LawId && l.Slug == slug));
        }
        // Görünürlüğü madde ve kanuna göre elenenler için biraz fazla çekilir.
        var take = Math.Clamp(count ?? 10, 1, MaxCount);
        var questions = await query.OrderBy(_ => EF.Functions.Random()).Take(take * 2).ToListAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, questions, includePrivate, ct)).Take(take).ToList());
    }

    /// <summary>Yönetim listesi: bütün sorular, kanuna göre süzülebilir.</summary>
    private static async Task<Ok<List<QuizQuestionDto>>> ListAsync(Guid? lawId, AppDbContext db, CancellationToken ct)
    {
        var questions = await db.QuizQuestions.AsNoTracking()
            .Where(q => lawId == null || q.LawId == lawId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(ct);
        return TypedResults.Ok(await ToDtosAsync(db, questions, includePrivate: true, ct));
    }

    private static async Task<Results<Created<QuizQuestionDto>, ValidationProblem>> CreateAsync(
        SaveQuizQuestionRequest request, AppDbContext db, CancellationToken ct)
    {
        var question = new QuizQuestion();
        if (await ApplyAsync(question, request, db, ct) is { } problem)
            return problem;
        db.QuizQuestions.Add(question);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/admin/quiz/{question.Id}", (await ToDtosAsync(db, [question], true, ct))[0]);
    }

    private static async Task<Results<Ok<QuizQuestionDto>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, SaveQuizQuestionRequest request, AppDbContext db, CancellationToken ct)
    {
        var question = await db.QuizQuestions.FindAsync([id], ct);
        if (question is null)
            return TypedResults.NotFound();
        if (await ApplyAsync(question, request, db, ct) is { } problem)
            return problem;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await ToDtosAsync(db, [question], true, ct))[0]);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var question = await db.QuizQuestions.FindAsync([id], ct);
        if (question is null)
            return TypedResults.NotFound();
        question.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<ValidationProblem?> ApplyAsync(QuizQuestion question, SaveQuizQuestionRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = new ValidationErrors().Required("text", request.Text, 2000).MaxLength("explanation", request.Explanation, 4000);
        var options = (request.Options ?? []).Select(InputText.Clean).ToList();
        if (options.Count is < QuizQuestion.MinOptions or > QuizQuestion.MaxOptions)
            errors.Add("options", $"{QuizQuestion.MinOptions} ile {QuizQuestion.MaxOptions} arasında şık girin.");
        else if (options.Any(o => o is null))
            errors.Add("options", "Şıklar boş olamaz.");
        else if (options.Any(o => o!.Length > 500))
            errors.Add("options", "Şık en fazla 500 karakter olabilir.");
        if (request.CorrectIndex < 0 || request.CorrectIndex >= options.Count)
            errors.Add("correctIndex", "Doğru şıkkı seçin.");
        if (!await db.Laws.AnyAsync(l => l.Id == request.LawId, ct))
            errors.Add("lawId", "Kanun bulunamadı.");
        else if (request.ArticleId is { } articleId && !await db.Articles.AnyAsync(a => a.Id == articleId && a.LawId == request.LawId, ct))
            errors.Add("articleId", "Madde bu kanunda bulunamadı.");
        if (errors.HasErrors)
            return errors.ToProblem();

        question.LawId = request.LawId;
        question.ArticleId = request.ArticleId;
        question.Text = InputText.CleanRequired(request.Text);
        question.Options = [.. options.Select(o => o!)];
        question.CorrectIndex = request.CorrectIndex;
        question.Explanation = InputText.Clean(request.Explanation);
        question.Visibility = request.Visibility;
        return null;
    }

    /// <summary>
    /// Sorular kanun kısaltması ve maddesiyle; anonimde private kanun/maddeye bağlı sorular elenir (maddenin
    /// görünürlüğü soruya geçer).
    /// </summary>
    private static async Task<List<QuizQuestionDto>> ToDtosAsync(
        AppDbContext db, List<QuizQuestion> questions, bool includePrivate, CancellationToken ct)
    {
        var lawIds = questions.Select(q => q.LawId).Distinct().ToList();
        var laws = await db.Laws.AsNoTracking().Where(l => lawIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var articleIds = questions.Where(q => q.ArticleId != null).Select(q => q.ArticleId!.Value).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToListAsync(ct);
        var visibleArticles = await VisibleArticles.FilterAsync(db, articles, includePrivate);

        var result = new List<QuizQuestionDto>();
        foreach (var q in questions)
        {
            if (!laws.TryGetValue(q.LawId, out var law) || (!includePrivate && law.Visibility == Visibility.Private))
                continue;
            LinkedArticleRef? article = null;
            if (q.ArticleId is { } articleId)
            {
                if (!visibleArticles.TryGetValue(articleId, out var visible))
                    continue;
                article = VisibleArticles.ToRef(visible.Article, visible.Law);
            }
            result.Add(new QuizQuestionDto(q.Id, law.Id, law.ShortName ?? law.Number, q.Text, [.. q.Options], q.CorrectIndex,
                q.Explanation, article, q.Visibility));
        }
        return result;
    }
}
