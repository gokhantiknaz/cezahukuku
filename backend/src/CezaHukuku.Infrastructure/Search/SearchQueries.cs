using System.Text;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Search;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CezaHukuku.Infrastructure.Search;

/// <param name="Kinds">Yalnız bu türler; null ise hepsi.</param>
/// <param name="LawIds">Yalnız bu kanunlarla ilgili sonuçlar; null ise hepsi.</param>
/// <param name="NodeId">Yalnız bu kitap/kısım/bölümün altındaki sonuçlar.</param>
/// <param name="OwnerId">Aramayı yapan kullanıcı: kişisel not ve vurgulardan yalnız onunkiler gelir. Anonimde null.</param>
/// <param name="IncludePrivate">Private içerik dahil mi (giriş yapmış kullanıcı).</param>
public sealed record SearchFilter(
    IReadOnlyCollection<SearchKind>? Kinds, IReadOnlyCollection<Guid>? LawIds, Guid? NodeId, Guid? OwnerId, bool IncludePrivate);

/// <param name="Snippet">Gövdeden kesit; eşleşen sözcükler <see cref="SearchQueries.MatchStart"/> ve
/// <see cref="SearchQueries.MatchEnd"/> karakterleri arasında.</param>
public sealed record SearchRow(SearchKind Kind, string Title, string? Context, string Url, DateOnly? Date, string Snippet);

public sealed record SearchPage(int Total, IReadOnlyDictionary<SearchKind, int> Counts, IReadOnlyList<SearchRow> Rows);

/// <summary>
/// Arama indeksinde (search_docs) sorgu. Her terim üç biçimde aranır: Türkçe kök ("öldürmenin" → öldürme), aksansız
/// önek ("oldurme:*") ve türetme ekleri atılmış aksansız gövde öneki ("oldur:*", <see cref="TurkishRoot"/>).
/// Sorgu ağacı parametreli tsquery ifadesine çevrilir; kullanıcı metni SQL'e hiçbir zaman doğrudan girmez.
/// </summary>
public static class SearchQueries
{
    public const char MatchStart = '\u0002';
    public const char MatchEnd = '\u0003';

    private sealed class RawRow
    {
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Context { get; set; }
        public string Url { get; set; } = "";
        public DateOnly? Date { get; set; }
        public string Snippet { get; set; } = "";
    }

    private sealed class RawCount
    {
        public string Kind { get; set; } = "";
        public int Count { get; set; }
    }

    public static async Task<SearchPage> SearchAsync(
        AppDbContext db, SearchNode query, SearchFilter filter, int offset, int limit, CancellationToken ct = default)
    {
        var parameters = new List<NpgsqlParameter>();
        string Parameter(object? value, NpgsqlDbType type)
        {
            var name = $"p{parameters.Count}";
            parameters.Add(new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });
            return "@" + name;
        }

        var tsquery = ToTsQuery(query, text => Parameter(text, NpgsqlDbType.Text));

        // Kişisel satırlar yalnız sahibine; diğerleri görünürlüğe göre.
        var where = new StringBuilder($"""
            d.search @@ q.query
            AND (d.owner_id IS NULL OR d.owner_id = {Parameter(filter.OwnerId, NpgsqlDbType.Uuid)})
            AND (d.owner_id IS NOT NULL OR {Parameter(filter.IncludePrivate, NpgsqlDbType.Boolean)} OR d.visibility = 'Public')
            """);
        if (filter.LawIds is { Count: > 0 } lawIds)
            where.Append($"\nAND d.law_ids && {Parameter(lawIds.ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Uuid)}");
        if (filter.NodeId is { } nodeId)
            where.Append($"\nAND {Parameter(nodeId, NpgsqlDbType.Uuid)} = ANY(d.node_ids)");
        var kindFilter = filter.Kinds is { Count: > 0 } kinds
            ? $"\nAND d.kind = ANY({Parameter(kinds.Select(k => k.ToString()).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text)})"
            : "";
        var limitParameter = Parameter(limit, NpgsqlDbType.Integer);
        var offsetParameter = Parameter(offset, NpgsqlDbType.Integer);
        var headlineOptions = Parameter(
            $"StartSel={MatchStart}, StopSel={MatchEnd}, MaxWords=35, MinWords=15, MaxFragments=2, FragmentDelimiter=\" … \"",
            NpgsqlDbType.Text);

        var countSql = $"""
            WITH q AS (SELECT {tsquery} AS query)
            SELECT d.kind AS "Kind", count(*)::int AS "Count"
            FROM search_docs d, q
            WHERE {where}
            GROUP BY d.kind
            """;
        var counts = (await db.Database.SqlQueryRaw<RawCount>(countSql, [.. parameters]).ToListAsync(ct))
            .ToDictionary(c => Enum.Parse<SearchKind>(c.Kind), c => c.Count);
        var total = filter.Kinds is { Count: > 0 }
            ? counts.Where(c => filter.Kinds.Contains(c.Key)).Sum(c => c.Value)
            : counts.Values.Sum();
        if (total == 0)
            return new SearchPage(0, counts, []);

        // Önce sıralanıp sayfa seçilir, kesit (pahalı) yalnız o sayfanın satırları için üretilir. Madde metni aynı
        // puandaki nottan önce gelir.
        var sql = $"""
            WITH q AS (SELECT {tsquery} AS query),
            hits AS (
                SELECT d.id, ts_rank_cd(d.search, q.query) * (CASE WHEN d.kind = 'Article' THEN 1.2 ELSE 1 END) AS rank
                FROM search_docs d, q
                WHERE {where}{kindFilter}
                ORDER BY rank DESC, d.title
                LIMIT {limitParameter} OFFSET {offsetParameter}
            )
            SELECT d.kind AS "Kind", d.title AS "Title", d.context AS "Context", d.url AS "Url", d.date AS "Date",
                   ts_headline('turkish', d.body, q.query, {headlineOptions}) AS "Snippet"
            FROM hits JOIN search_docs d ON d.id = hits.id, q
            ORDER BY hits.rank DESC, d.title
            """;
        var rows = await db.Database.SqlQueryRaw<RawRow>(sql, [.. CloneAll(parameters)]).ToListAsync(ct);
        var prefixes = SnippetMarker.Prefixes(query);
        return new SearchPage(
            total,
            counts,
            [
                .. rows.Select(r => new SearchRow(Enum.Parse<SearchKind>(r.Kind), r.Title, r.Context, r.Url, r.Date,
                    SnippetMarker.Mark(r.Snippet, prefixes, MatchStart, MatchEnd))),
            ]);
    }

    /// <summary>Sorgu ağacını tsquery üreten SQL ifadesine çevirir; metinler parametre olarak verilir.</summary>
    public static string ToTsQuery(SearchNode node, Func<string, string> parameter) => node switch
    {
        SearchTerm term => "(" + string.Join(" || ", TermQueries(term, parameter)) + ")",
        SearchPhrase phrase =>
            $"(phraseto_tsquery('turkish', {parameter(string.Join(' ', phrase.Normalized))})"
            + $" || phraseto_tsquery('simple', {parameter(string.Join(' ', phrase.Folded))}))",
        SearchAnd and => $"({ToTsQuery(and.Left, parameter)} && {ToTsQuery(and.Right, parameter)})",
        SearchOr or => $"({ToTsQuery(or.Left, parameter)} || {ToTsQuery(or.Right, parameter)})",
        SearchNot not => $"!!{ToTsQuery(not.Inner, parameter)}",
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    private static IEnumerable<string> TermQueries(SearchTerm term, Func<string, string> parameter)
    {
        // Terim yalnız harf ve rakamdan oluşur (SearchQueryParser); ":*" önek işaretini burada eklemek güvenli.
        yield return $"to_tsquery('turkish', {parameter(term.Normalized)})";
        // Çok kısa sözcükte önek ("ce:*") her şeyi getirir; tam sözcük aranır.
        yield return $"to_tsquery('simple', {parameter(term.Folded.Length >= 3 ? term.Folded + ":*" : term.Folded)})";
        if (term.Root is { } root)
            yield return $"to_tsquery('simple', {parameter(root + ":*")})";
    }

    // Npgsql aynı parametre nesnesinin iki komutta kullanılmasına izin vermez.
    private static IEnumerable<NpgsqlParameter> CloneAll(List<NpgsqlParameter> parameters) =>
        parameters.Select(p => p.Clone());
}
