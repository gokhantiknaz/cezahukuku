using System.Text;
using CezaHukuku.Parsing.Text;

namespace CezaHukuku.Parsing.Search;

/// <summary>
/// Arama kesitinde eşleşen sözcükleri işaretler. Veritabanının kesiti (ts_headline) yalnız kök eşleşmelerini
/// işaretler; sorgu önek eşleşmesiyle de sonuç bulduğundan ("öldüren" ← "öldürme", "oldurme" ← "öldürme") aksansız
/// biçimi sorgu terimlerinden biriyle başlayan sözcükler de işaretlenir.
/// </summary>
public static class SnippetMarker
{
    /// <summary>Sorgunun olumlu terimlerinin aksansız önekleri (hariç tutulanlar işaretlenmez).</summary>
    public static IReadOnlyList<string> Prefixes(SearchNode node)
    {
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        void Visit(SearchNode n)
        {
            switch (n)
            {
                case SearchTerm term:
                    if (term.Folded.Length >= 3) prefixes.Add(term.Folded);
                    if (term.Root is { } root) prefixes.Add(root);
                    break;
                case SearchPhrase phrase:
                    foreach (var word in phrase.Folded.Where(w => w.Length >= 3)) prefixes.Add(word);
                    break;
                case SearchAnd and: Visit(and.Left); Visit(and.Right); break;
                case SearchOr or: Visit(or.Left); Visit(or.Right); break;
            }
        }
        Visit(node);
        return [.. prefixes];
    }

    /// <summary>
    /// <paramref name="start"/>/<paramref name="end"/> işaretleri arasında olmayan ve aksansız biçimi öneklerden biriyle
    /// başlayan sözcükleri işaretler.
    /// </summary>
    public static string Mark(string snippet, IReadOnlyList<string> prefixes, char start, char end)
    {
        if (prefixes.Count == 0 || snippet.Length == 0)
            return snippet;

        var result = new StringBuilder(snippet.Length + 16);
        var inside = false;
        var i = 0;
        while (i < snippet.Length)
        {
            var c = snippet[i];
            if (c == start || c == end)
            {
                inside = c == start;
                result.Append(c);
                i++;
                continue;
            }
            if (!char.IsLetterOrDigit(c))
            {
                result.Append(c);
                i++;
                continue;
            }

            var wordStart = i;
            while (i < snippet.Length && char.IsLetterOrDigit(snippet[i]))
                i++;
            var word = snippet[wordStart..i];
            var folded = TurkishText.Fold(word);
            if (!inside && prefixes.Any(p => folded.StartsWith(p, StringComparison.Ordinal)))
                result.Append(start).Append(word).Append(end);
            else
                result.Append(word);
        }
        return result.ToString();
    }
}
