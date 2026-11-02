using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Personal;

/// <param name="Quote">Vurgulanan metin (parçalar " … " ile birleşik).</param>
/// <param name="IsStale">Vurgu maddenin yürürlükteki metninde bulunamadı (madde değişmiş).</param>
/// <param name="Note">Vurguya bağlı not; yoksa null.</param>
public sealed record NotebookHighlightDto(
    Guid Id, HighlightColor Color, string Quote, bool IsStale, PersonalNoteDto? Note, DateTimeOffset CreatedAt);

/// <param name="Highlights">Maddedeki vurgularım, yürürlükteki metindeki sırasıyla (bulunamayanlar sonda).</param>
/// <param name="Notes">Maddenin geneline yazdığım notlar, eskiden yeniye.</param>
/// <param name="LastChangedAt">Maddedeki vurgu ve notlarımdan en son eklenen ya da değiştirilenin zamanı.</param>
public sealed record NotebookArticleDto(
    LinkedArticleRef Article,
    IReadOnlyList<NotebookHighlightDto> Highlights,
    IReadOnlyList<PersonalNoteDto> Notes,
    DateTimeOffset LastChangedAt);

/// <summary>Bütün maddelerdeki vurgu ve notlarım tek listede (Notlarım sayfası); yalnız sahibi görür.</summary>
public static class NotebookEndpoints
{
    public static RouteGroupBuilder MapNotebookEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/notebook", ListAsync).WithName("GetNotebook");
        return group;
    }

    /// <summary>Vurgu ya da notum olan maddeler, kütüphane sırasıyla (kanun, sonra madde sırası). Silinmiş maddeler çıkmaz.</summary>
    private static async Task<Ok<List<NotebookArticleDto>>> ListAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var userId = principal.UserId();
        var highlights = await db.Highlights.AsNoTracking().Where(h => h.OwnerId == userId).OrderBy(h => h.CreatedAt).ToListAsync(ct);
        var notes = await db.PersonalNotes.AsNoTracking().Where(n => n.OwnerId == userId).OrderBy(n => n.CreatedAt).ToListAsync(ct);

        var ids = highlights.Select(h => h.ArticleId).Concat(notes.Select(n => n.ArticleId)).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        var visible = await VisibleArticles.FilterAsync(db, articles, includePrivate: true);
        var versions = (await db.ArticleVersions.AsNoTracking().Where(v => ids.Contains(v.ArticleId)).ToListAsync(ct))
            .ToLookup(v => v.ArticleId);
        var highlightsByArticle = highlights.ToLookup(h => h.ArticleId);
        var notesByArticle = notes.ToLookup(n => n.ArticleId);

        var result = visible.Values
            .OrderBy(v => v.Law.SortOrder).ThenBy(v => v.Law.Name).ThenBy(v => v.Article.SortOrder)
            .Select(v =>
            {
                var articleId = v.Article.Id;
                var articleNotes = notesByArticle[articleId].ToList();
                var noteOf = articleNotes.Where(n => n.HighlightId is not null).ToDictionary(n => n.HighlightId!.Value);
                var paragraphs = ArticleVersion.Select(versions[articleId], null)?.Paragraphs ?? [];
                var entries = highlightsByArticle[articleId]
                    .Select(h => ToDto(h, paragraphs, noteOf.GetValueOrDefault(h.Id)))
                    .OrderBy(e => e.Position)
                    .Select(e => e.Dto)
                    .ToList();
                var lastChangedAt = highlightsByArticle[articleId].Select(h => h.UpdatedAt)
                    .Concat(articleNotes.Select(n => n.UpdatedAt))
                    .Max();
                return new NotebookArticleDto(
                    VisibleArticles.ToRef(v.Article, v.Law),
                    entries,
                    [.. articleNotes.Where(n => n.HighlightId is null).Select(PersonalNoteEndpoints.ToDto)],
                    lastChangedAt);
            })
            .ToList();
        return TypedResults.Ok(result);
    }

    /// <summary>Vurgu ve metindeki yeri: (fıkra/bent sırası, başlangıç); metinde bulunamazsa en sona.</summary>
    private static (NotebookHighlightDto Dto, (int Block, int Start) Position) ToDto(
        Highlight highlight, IReadOnlyList<Paragraph> paragraphs, PersonalNote? note)
    {
        var located = highlight.Segments.Select(s => TextAnchoring.Locate(s, paragraphs)).ToList();
        var isStale = located.Any(p => p is null);
        var first = located.FirstOrDefault();
        var position = isStale || first is null ? (int.MaxValue, 0) : (BlockIndex(paragraphs, first), first.Start);
        var dto = new NotebookHighlightDto(
            highlight.Id,
            highlight.Color,
            string.Join(" … ", highlight.Segments.Select(s => s.Quote)),
            isStale,
            note is null ? null : PersonalNoteEndpoints.ToDto(note),
            highlight.CreatedAt);
        return (dto, position);
    }

    private static int BlockIndex(IReadOnlyList<Paragraph> paragraphs, TextPosition position)
    {
        var index = 0;
        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Id == position.ParagraphId && position.ItemId is null)
                return index;
            index++;
            foreach (var item in paragraph.Items)
            {
                if (item.Id == position.ItemId)
                    return index;
                index++;
            }
        }
        return int.MaxValue;
    }
}
