using System.Text;
using CezaHukuku.Parsing.Text;

namespace CezaHukuku.Parsing.Search;

/// <summary>Arama sorgusunun ağacı.</summary>
public abstract record SearchNode;

/// <summary>
/// Tek sözcük. <see cref="Normalized"/> Türkçe küçük harf (kök bulucuya gider), <see cref="Folded"/> aksansız
/// (önek eşleşmesi), <see cref="Root"/> türetme ekleri atılmış aksansız gövde (önek eşleşmesi; "öldüren" → "oldur").
/// </summary>
public sealed record SearchTerm(string Normalized, string Folded, string? Root) : SearchNode;

/// <summary>Tırnak içindeki ifade: sözcükler bu sırayla yan yana geçmeli.</summary>
public sealed record SearchPhrase(IReadOnlyList<string> Normalized, IReadOnlyList<string> Folded) : SearchNode;

public sealed record SearchAnd(SearchNode Left, SearchNode Right) : SearchNode;

public sealed record SearchOr(SearchNode Left, SearchNode Right) : SearchNode;

public sealed record SearchNot(SearchNode Inner) : SearchNode;

/// <summary>
/// Arama sorgu dili: <c>"tam ifade"</c>, <c>VE</c>/<c>AND</c>/<c>&amp;</c> (yan yana yazmak da VE), <c>VEYA</c>/<c>OR</c>/<c>|</c>,
/// <c>-hariç</c>/<c>DEĞİL</c>/<c>NOT</c>, parantez. Öncelik: DEĞİL &gt; VE &gt; VEYA. Sözcükler harf ve rakamlardan oluşur;
/// "103/a" gibi bitişik yazılan parçalar ifade sayılır. Olumlu terimi olmayan sorgu (yalnız hariç) boş döner.
/// </summary>
public static class SearchQueryParser
{
    public static SearchNode? Parse(string? query)
    {
        var tokens = Tokenize(query ?? "");
        var position = 0;
        var node = ParseOr(tokens, ref position);
        return node is not null && HasPositive(node) ? node : null;
    }

    private enum TokenKind { Word, Phrase, And, Or, Not, Open, Close }

    private sealed record Token(TokenKind Kind, string Text = "");

    private static List<Token> Tokenize(string query)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < query.Length)
        {
            var c = query[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            switch (c)
            {
                case '(':
                    tokens.Add(new Token(TokenKind.Open)); i++; continue;
                case ')':
                    tokens.Add(new Token(TokenKind.Close)); i++; continue;
                case '&':
                    tokens.Add(new Token(TokenKind.And)); i++; continue;
                case '|':
                    tokens.Add(new Token(TokenKind.Or)); i++; continue;
                case '"' or '“' or '”':
                {
                    var end = query.IndexOfAny(['"', '“', '”'], i + 1);
                    if (end < 0) end = query.Length;
                    tokens.Add(new Token(TokenKind.Phrase, query[(i + 1)..end]));
                    i = end + 1;
                    continue;
                }
                case '-' when i + 1 < query.Length && !char.IsWhiteSpace(query[i + 1]):
                    tokens.Add(new Token(TokenKind.Not)); i++; continue;
            }

            var start = i;
            while (i < query.Length && !char.IsWhiteSpace(query[i]) && query[i] is not ('(' or ')' or '"' or '“' or '”' or '&' or '|'))
                i++;
            var word = query[start..i];
            tokens.Add(TurkishText.Normalize(word) switch
            {
                "ve" or "and" => new Token(TokenKind.And),
                "veya" or "or" => new Token(TokenKind.Or),
                "değil" or "degil" or "not" => new Token(TokenKind.Not),
                _ => new Token(TokenKind.Word, word),
            });
        }
        return tokens;
    }

    private static SearchNode? ParseOr(List<Token> tokens, ref int position)
    {
        var left = ParseAnd(tokens, ref position);
        while (position < tokens.Count && tokens[position].Kind == TokenKind.Or)
        {
            position++;
            var right = ParseAnd(tokens, ref position);
            left = Combine(left, right, (l, r) => new SearchOr(l, r));
        }
        return left;
    }

    private static SearchNode? ParseAnd(List<Token> tokens, ref int position)
    {
        SearchNode? left = null;
        while (position < tokens.Count && tokens[position].Kind is not (TokenKind.Or or TokenKind.Close))
        {
            if (tokens[position].Kind == TokenKind.And)
            {
                position++;
                continue;
            }
            var right = ParseUnary(tokens, ref position);
            left = Combine(left, right, (l, r) => new SearchAnd(l, r));
        }
        return left;
    }

    private static SearchNode? ParseUnary(List<Token> tokens, ref int position)
    {
        var token = tokens[position++];
        switch (token.Kind)
        {
            case TokenKind.Not:
                return position < tokens.Count && ParseUnary(tokens, ref position) is { } inner ? new SearchNot(inner) : null;
            case TokenKind.Open:
                var group = ParseOr(tokens, ref position);
                if (position < tokens.Count && tokens[position].Kind == TokenKind.Close)
                    position++;
                return group;
            case TokenKind.Word or TokenKind.Phrase:
                return Leaf(token.Text);
            default:
                return null; // fazladan ")" ya da operatör: yok sayılır
        }
    }

    /// <summary>Metni harf/rakam parçalarına böler: tek parça terim, çok parça ifade olur.</summary>
    private static SearchNode? Leaf(string text)
    {
        var words = Words(TurkishText.Normalize(text));
        return words.Count switch
        {
            0 => null,
            1 => Term(words[0]), // tırnak içindeki tek sözcük de terimdir
            _ => new SearchPhrase(words, [.. words.Select(w => TurkishText.Fold(w))]),
        };
    }

    private static SearchTerm Term(string word)
    {
        var folded = TurkishText.Fold(word);
        var root = TurkishRoot.Guess(folded);
        return new SearchTerm(word, folded, root != folded ? root : null);
    }

    /// <summary>Normalize edilmiş metnin sözcükleri (harf ve rakam dizileri).</summary>
    public static List<string> Words(string normalized)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
                continue;
            }
            if (current.Length > 0)
                words.Add(current.ToString());
            current.Clear();
        }
        if (current.Length > 0)
            words.Add(current.ToString());
        return words;
    }

    private static SearchNode? Combine(SearchNode? left, SearchNode? right, Func<SearchNode, SearchNode, SearchNode> make) =>
        (left, right) switch
        {
            (null, _) => right,
            (_, null) => left,
            _ => make(left, right),
        };

    /// <summary>Sorgu en az bir olumlu terim içeriyor mu? ("-taksir" tek başına bütün metinleri döndürmesin.)</summary>
    private static bool HasPositive(SearchNode node) => node switch
    {
        SearchTerm or SearchPhrase => true,
        SearchNot => false,
        SearchAnd and => HasPositive(and.Left) || HasPositive(and.Right),
        SearchOr or => HasPositive(or.Left) && HasPositive(or.Right),
        _ => false,
    };
}
