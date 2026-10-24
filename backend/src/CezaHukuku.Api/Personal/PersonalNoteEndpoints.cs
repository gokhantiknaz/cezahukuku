using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Personal;

/// <param name="HighlightId">Bağlı olduğu vurgu; maddenin geneline yazılmış notta null.</param>
public sealed record PersonalNoteDto(
    Guid Id, Guid ArticleId, Guid? HighlightId, string Text, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <param name="HighlightId">Verilirse not bu vurguya bağlanır (vurgu başına tek not).</param>
public sealed record CreatePersonalNoteRequest(Guid ArticleId, Guid? HighlightId, string? Text);

public sealed record UpdatePersonalNoteRequest(string? Text);

/// <summary>Kişisel notlar: yalnız sahibi görür ve değiştirir (başkasının notu ve vurgusu 404).</summary>
public static class PersonalNoteEndpoints
{
    public static RouteGroupBuilder MapPersonalNoteEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/articles/{articleId:guid}/notes", ListAsync).WithName("ListPersonalNotes");
        group.MapPost("/notes", CreateAsync).WithName("CreatePersonalNote");
        group.MapPut("/notes/{id:guid}", UpdateAsync).WithName("UpdatePersonalNote");
        group.MapDelete("/notes/{id:guid}", DeleteAsync).WithName("DeletePersonalNote");
        return group;
    }

    /// <summary>Maddedeki notlarım (vurguya bağlı olanlar ve maddenin geneline yazılanlar), eskiden yeniye.</summary>
    private static async Task<Ok<List<PersonalNoteDto>>> ListAsync(Guid articleId, ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.UserId();
        var notes = await db.PersonalNotes.AsNoTracking()
            .Where(n => n.OwnerId == userId && n.ArticleId == articleId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();
        return TypedResults.Ok(notes.Select(ToDto).ToList());
    }

    private static async Task<Results<Created<PersonalNoteDto>, NotFound, ValidationProblem>> CreateAsync(
        CreatePersonalNoteRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.UserId();
        if (!await db.Articles.AnyAsync(a => a.Id == request.ArticleId))
            return TypedResults.NotFound();
        if (request.HighlightId is { } highlightId)
        {
            if (!await db.Highlights.AnyAsync(h => h.Id == highlightId && h.OwnerId == userId && h.ArticleId == request.ArticleId))
                return TypedResults.NotFound();
            if (await db.PersonalNotes.AnyAsync(n => n.HighlightId == highlightId))
                return ValidationErrors.Single("highlightId", "Bu vurgunun zaten bir notu var.");
        }
        if (Validate(request.Text, out var text) is { } problem)
            return problem;

        var note = new PersonalNote { OwnerId = userId, ArticleId = request.ArticleId, HighlightId = request.HighlightId, Text = text };
        db.PersonalNotes.Add(note);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/me/notes/{note.Id}", ToDto(note));
    }

    private static async Task<Results<Ok<PersonalNoteDto>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, UpdatePersonalNoteRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.UserId();
        var note = await db.PersonalNotes.FirstOrDefaultAsync(n => n.Id == id && n.OwnerId == userId);
        if (note is null)
            return TypedResults.NotFound();
        if (Validate(request.Text, out var text) is { } problem)
            return problem;

        note.Text = text;
        await db.SaveChangesAsync();
        return TypedResults.Ok(ToDto(note));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock)
    {
        var userId = principal.UserId();
        var note = await db.PersonalNotes.FirstOrDefaultAsync(n => n.Id == id && n.OwnerId == userId);
        if (note is null)
            return TypedResults.NotFound();

        note.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    /// <summary>Metin NFC'ye çevrilir, baş/son boşluklar atılır, satır sonları "\n" olur.</summary>
    private static ValidationProblem? Validate(string? input, out string text)
    {
        text = InputText.CleanRequired(input?.Replace("\r\n", "\n"));
        if (text.Length == 0)
            return ValidationErrors.Single("text", "Not boş olamaz.");
        if (text.Length > PersonalNote.MaxTextLength)
            return ValidationErrors.Single("text", $"Not en fazla {PersonalNote.MaxTextLength} karakter olabilir.");
        return null;
    }

    private static PersonalNoteDto ToDto(PersonalNote note) =>
        new(note.Id, note.ArticleId, note.HighlightId, note.Text, note.CreatedAt, note.UpdatedAt);
}
