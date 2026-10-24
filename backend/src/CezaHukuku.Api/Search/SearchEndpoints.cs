using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Users;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Infrastructure.Search;
using CezaHukuku.Parsing.Legislation;
using CezaHukuku.Parsing.Search;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Search;

/// <summary>Sorgu doğrudan bir maddeyi gösteriyor ("tck 81", "5237/81"): sonuçların üstünde "maddeye git".</summary>
public sealed record SearchJump(string Label, string Url);

/// <param name="Snippet">Gövdeden kesit; eşleşen sözcükler "\u0002" ile "\u0003" arasında.</param>
public sealed record SearchHit(SearchKind Kind, string Title, string? Context, string Url, DateOnly? Date, string Snippet);

public sealed record SearchKindCount(SearchKind Kind, int Count);

/// <param name="Total">Seçili türlerdeki toplam sonuç (sayfalama için).</param>
/// <param name="Counts">Tür filtresi uygulanmadan türlere göre sonuç sayıları (filtre düğmelerinde).</param>
public sealed record SearchResponse(string Query, SearchJump? Jump, int Total, List<SearchKindCount> Counts, List<SearchHit> Hits);

public static class SearchEndpoints
{
    private const int MaxQueryLength = 200;
    private const int MaxLimit = 50;

    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search", SearchAsync).WithTags("Search").WithName("Search").AllowAnonymous();
        app.MapPost("/api/admin/search/rebuild", RebuildAsync)
            .WithTags("Search").WithName("RebuildSearchIndex").RequireAuthorization(Policies.AdminOnly);
    }

    /// <summary>
    /// Arama. Anonim kullanıcı yalnız herkese açık içeriği, giriş yapmış kullanıcı private içeriği ve kendi not ve
    /// vurgularını da bulur.
    /// </summary>
    /// <param name="q">Sorgu: sözcükler, "tam ifade", VE / VEYA / -hariç, parantez; "tck 81" doğrudan maddeye gider.</param>
    /// <param name="kinds">Yalnız bu türler: article, gerekce, aciklama, dersNotu, genelGerekce, ictihat, normDenetimi, note, highlight.</param>
    /// <param name="laws">Yalnız bu kanunlar (adres adı: "tck").</param>
    /// <param name="node">Yalnız bu kitap/kısım/bölümün altı.</param>
    /// <param name="mine">Yalnız kendi not ve vurgularım.</param>
    private static async Task<Ok<SearchResponse>> SearchAsync(
        string? q, string[]? kinds, string[]? laws, Guid? node, bool? mine, int? offset, int? limit,
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var text = (q ?? "").Trim();
        if (text.Length > MaxQueryLength)
            text = text[..MaxQueryLength];
        var includePrivate = principal.CanSeePrivate();
        var jump = await FindJumpAsync(db, text, includePrivate, ct);
        var query = SearchQueryParser.Parse(text);
        if (query is null)
            return TypedResults.Ok(new SearchResponse(text, jump, 0, [], []));

        var kindFilter = (kinds ?? [])
            .SelectMany(k => k.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(k => Enum.TryParse<SearchKind>(k, ignoreCase: true, out var kind) ? kind : (SearchKind?)null)
            .OfType<SearchKind>()
            .ToHashSet();
        if (mine == true)
        {
            kindFilter = kindFilter.Count == 0 ? [SearchKind.Note, SearchKind.Highlight] : [.. kindFilter.Where(k => k is SearchKind.Note or SearchKind.Highlight)];
            if (kindFilter.Count == 0)
                return TypedResults.Ok(new SearchResponse(text, jump, 0, [], []));
        }

        List<Guid>? lawIds = null;
        if (laws is { Length: > 0 })
        {
            var slugs = laws.SelectMany(l => l.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(s => s.ToLowerInvariant()).ToList();
            lawIds = await db.Laws.Where(l => slugs.Contains(l.Slug)).Select(l => l.Id).ToListAsync(ct);
            if (lawIds.Count == 0)
                return TypedResults.Ok(new SearchResponse(text, jump, 0, [], []));
        }

        // Kişisel kayıtlar yalnız kişisel katmanı olan rollerde (reader'ın notu olmaz).
        Guid? ownerId = principal.IsInRole(Roles.SuperAdmin) || principal.IsInRole(Roles.Standard) ? principal.UserId() : null;
        var page = await SearchQueries.SearchAsync(
            db,
            query,
            new SearchFilter(kindFilter.Count > 0 ? kindFilter : null, lawIds, node, ownerId, includePrivate),
            Math.Max(offset ?? 0, 0),
            Math.Clamp(limit ?? 20, 1, MaxLimit),
            ct);

        return TypedResults.Ok(new SearchResponse(
            text,
            jump,
            page.Total,
            [.. page.Counts.OrderBy(c => c.Key).Select(c => new SearchKindCount(c.Key, c.Value))],
            [.. page.Rows.Select(r => new SearchHit(r.Kind, r.Title, r.Context, r.Url, r.Date, r.Snippet))]));
    }

    /// <summary>"tck 81", "5237/81", "cmk geçici 1": kanun kısa adı, numarası ya da adresiyle madde bulunursa.</summary>
    private static async Task<SearchJump?> FindJumpAsync(AppDbContext db, string text, bool includePrivate, CancellationToken ct)
    {
        if (ArticleReferenceParser.Parse(text) is not { } reference)
            return null;

        var laws = await db.Laws.AsNoTracking().ToListAsync(ct);
        var law = laws.FirstOrDefault(l => l.ShortName is not null && TurkishText.Fold(l.ShortName) == reference.Law)
                  ?? laws.FirstOrDefault(l => l.Number == reference.Law)
                  ?? laws.FirstOrDefault(l => l.Slug == reference.Law);
        if (law is null)
            return null;

        var slug = Slugs.ForArticle(reference.ArticleNumber);
        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.LawId == law.Id && a.Slug == slug, ct);
        if (article is null)
            return null;
        var nodes = await db.LawNodes.AsNoTracking().Where(n => n.LawId == law.Id).ToListAsync(ct);
        if (!includePrivate && LawOutline.Build(law, nodes, [], includePrivate: true).EffectiveVisibilityOf(article) != Visibility.Public)
            return null;

        var label = $"{law.ShortName ?? law.Number} Madde {article.Number}";
        return new SearchJump(string.IsNullOrWhiteSpace(article.Title) ? label : $"{label} — {article.Title}", $"/{law.Slug}/{article.Slug}");
    }

    private static async Task<Ok<int>> RebuildAsync(AppDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await SearchIndexer.RebuildAllAsync(db, ct));
}
