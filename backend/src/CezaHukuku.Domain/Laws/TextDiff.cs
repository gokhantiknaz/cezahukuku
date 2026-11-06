using System.Text;
using System.Text.RegularExpressions;

namespace CezaHukuku.Domain.Laws;

public enum DiffKind
{
    Same,
    Inserted,
    Deleted,
}

public sealed record DiffPiece(DiffKind Kind, string Text);

/// <summary>
/// İki metnin kelime düzeyinde farkı: değişen yerler silinen ve eklenen parçalar olarak, aynı kalanlar arada.
/// Değişen kelimeler arasındaki tek boşluk değişikliğe katılır ("üç ay" → "beş yıl" iki ayrı değil, tek değişiklik);
/// bir değişiklikte önce silinen, sonra eklenen parça gelir.
/// </summary>
public static partial class TextDiff
{
    /// <summary>Bundan büyük karşılaştırmada (kelime sayılarının çarpımı) ortadaki kısım bütün olarak değişmiş sayılır.</summary>
    private const long MaxCells = 4_000_000;

    [GeneratedRegex(@"\w+|\s+|[^\w\s]")]
    private static partial Regex Token();

    public static IReadOnlyList<DiffPiece> Words(string before, string after)
    {
        var a = Tokens(before);
        var b = Tokens(after);
        var prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix])
            prefix++;
        var suffix = 0;
        while (suffix < a.Length - prefix && suffix < b.Length - prefix && a[^(suffix + 1)] == b[^(suffix + 1)])
            suffix++;

        var ops = new List<(DiffKind Kind, string Token)>();
        ops.AddRange(a[..prefix].Select(t => (DiffKind.Same, t)));
        ops.AddRange(Middle(a[prefix..^suffix], b[prefix..^suffix]));
        ops.AddRange(a[^suffix..].Select(t => (DiffKind.Same, t)));
        return Group(ops);
    }

    private static string[] Tokens(string text) => [.. Token().Matches(text).Select(m => m.Value)];

    /// <summary>En uzun ortak alt dizi ile kelime kelime fark.</summary>
    private static IEnumerable<(DiffKind, string)> Middle(string[] a, string[] b)
    {
        if ((long)a.Length * b.Length > MaxCells)
            return [.. a.Select(t => (DiffKind.Deleted, t)), .. b.Select(t => (DiffKind.Inserted, t))];

        // lcs[i, j]: a[i..] ile b[j..] arasındaki en uzun ortak alt dizi.
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var ops = new List<(DiffKind, string)>(a.Length + b.Length);
        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y])
            {
                ops.Add((DiffKind.Same, a[x++]));
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
                ops.Add((DiffKind.Deleted, a[x++]));
            else
                ops.Add((DiffKind.Inserted, b[y++]));
        }
        ops.AddRange(a[x..].Select(t => (DiffKind.Deleted, t)));
        ops.AddRange(b[y..].Select(t => (DiffKind.Inserted, t)));
        return ops;
    }

    private static List<DiffPiece> Group(List<(DiffKind Kind, string Token)> ops)
    {
        var pieces = new List<DiffPiece>();
        var same = new StringBuilder();
        var deleted = new StringBuilder();
        var inserted = new StringBuilder();

        void FlushChange()
        {
            if (deleted.Length > 0)
                pieces.Add(new DiffPiece(DiffKind.Deleted, deleted.ToString()));
            if (inserted.Length > 0)
                pieces.Add(new DiffPiece(DiffKind.Inserted, inserted.ToString()));
            deleted.Clear();
            inserted.Clear();
        }

        for (var i = 0; i < ops.Count; i++)
        {
            var (kind, token) = ops[i];
            // İki değişiklik arasındaki boşluk ikisine birden katılır.
            var joinsChanges = kind == DiffKind.Same && string.IsNullOrWhiteSpace(token)
                && deleted.Length + inserted.Length > 0
                && i + 1 < ops.Count && ops[i + 1].Kind != DiffKind.Same;
            if (kind == DiffKind.Same && !joinsChanges)
            {
                FlushChange();
                same.Append(token);
                continue;
            }
            if (same.Length > 0)
            {
                pieces.Add(new DiffPiece(DiffKind.Same, same.ToString()));
                same.Clear();
            }
            if (kind != DiffKind.Inserted)
                deleted.Append(token);
            if (kind != DiffKind.Deleted)
                inserted.Append(token);
        }
        FlushChange();
        if (same.Length > 0)
            pieces.Add(new DiffPiece(DiffKind.Same, same.ToString()));
        return pieces;
    }
}
