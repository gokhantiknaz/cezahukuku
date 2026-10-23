using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Content;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <param name="Html">Düzenleyiciden gelen HTML; sunucuda izin verilen etiketlere indirgenir.</param>
public sealed record SaveEditorialNoteRequest(EditorialNoteKind Kind, string? Title, string? Html, Visibility Visibility, int? SortOrder);

/// <summary>Editoryal notlar: maddeye (gerekçe, açıklama, ders notu) ya da kanuna (genel gerekçe, ders notu) bağlı.</summary>
internal static class AdminNoteEndpoints
{
    private const int MaxHtmlLength = 200_000;

    public static void MapNoteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/articles/{articleId:guid}/notes", CreateForArticleAsync).WithName("CreateArticleNote");
        group.MapPost("/laws/{lawId:guid}/notes", CreateForLawAsync).WithName("CreateLawNote");
        group.MapPut("/notes/{id:guid}", UpdateAsync).WithName("UpdateEditorialNote");
        group.MapDelete("/notes/{id:guid}", DeleteAsync).WithName("DeleteEditorialNote");
    }

    private static async Task<Results<Created<EditorialNoteDto>, NotFound, ValidationProblem>> CreateForArticleAsync(
        Guid articleId, SaveEditorialNoteRequest request, AppDbContext db)
    {
        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == articleId);
        if (article is null)
            return TypedResults.NotFound();
        return await CreateAsync(new EditorialNote { LawId = article.LawId, ArticleId = article.Id }, request, db);
    }

    private static async Task<Results<Created<EditorialNoteDto>, NotFound, ValidationProblem>> CreateForLawAsync(
        Guid lawId, SaveEditorialNoteRequest request, AppDbContext db)
    {
        if (!await db.Laws.AnyAsync(l => l.Id == lawId))
            return TypedResults.NotFound();
        return await CreateAsync(new EditorialNote { LawId = lawId }, request, db);
    }

    private static async Task<Results<Created<EditorialNoteDto>, NotFound, ValidationProblem>> CreateAsync(
        EditorialNote note, SaveEditorialNoteRequest request, AppDbContext db)
    {
        if (Apply(note, request) is { } problem)
            return problem;

        note.SortOrder = request.SortOrder
            ?? (await db.EditorialNotes.Where(n => n.LawId == note.LawId && n.ArticleId == note.ArticleId && n.Kind == note.Kind)
                .MaxAsync(n => (int?)n.SortOrder) ?? 0) + 1;
        db.EditorialNotes.Add(note);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/admin/notes/{note.Id}", await ToDtoAsync(note, db));
    }

    private static async Task<Results<Ok<EditorialNoteDto>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, SaveEditorialNoteRequest request, AppDbContext db)
    {
        var note = await db.EditorialNotes.FindAsync(id);
        if (note is null)
            return TypedResults.NotFound();
        if (Apply(note, request) is { } problem)
            return problem;

        note.SortOrder = request.SortOrder ?? note.SortOrder;
        await db.SaveChangesAsync();
        return TypedResults.Ok(await ToDtoAsync(note, db));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid id, AppDbContext db, TimeProvider clock)
    {
        var note = await db.EditorialNotes.FindAsync(id);
        if (note is null)
            return TypedResults.NotFound();

        note.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static ValidationProblem? Apply(EditorialNote note, SaveEditorialNoteRequest request)
    {
        var errors = new ValidationErrors().MaxLength("title", request.Title, 500);

        if (note.ArticleId is null && !EditorialNote.IsLawLevel(request.Kind))
            errors.Add("kind", "Kanun düzeyinde yalnız genel gerekçe ve ders notu eklenebilir.");
        else if (note.ArticleId is not null && !EditorialNote.IsArticleLevel(request.Kind))
            errors.Add("kind", "Genel gerekçe maddeye değil kanuna eklenir.");

        if (request.Html is { Length: > MaxHtmlLength })
            errors.Add("html", "Metin çok uzun.");
        var html = RichText.Sanitize(request.Html);
        if (html.Length == 0)
            errors.Add("html", "Not metni boş olamaz.");

        if (errors.HasErrors)
            return errors.ToProblem();

        note.Kind = request.Kind;
        note.Title = InputText.Clean(request.Title);
        note.Html = html;
        note.PlainText = RichText.ToPlainText(html);
        note.Visibility = request.Visibility;
        return null;
    }

    /// <summary>Geçerli görünürlük bağlı olduğu madde ya da kanunla daraltılarak hesaplanır.</summary>
    private static async Task<EditorialNoteDto> ToDtoAsync(EditorialNote note, AppDbContext db)
    {
        var law = await db.Laws.AsNoTracking().FirstAsync(l => l.Id == note.LawId);
        var parent = law.Visibility;
        if (note.ArticleId is { } articleId)
        {
            var article = await db.Articles.AsNoTracking().FirstAsync(a => a.Id == articleId);
            var nodes = await db.LawNodes.AsNoTracking().Where(n => n.LawId == law.Id).ToListAsync();
            parent = LawOutline.Build(law, nodes, [], includePrivate: true).EffectiveVisibilityOf(article);
        }
        return note.ToDto(parent.Narrow(note.Visibility));
    }
}
