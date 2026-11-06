namespace CezaHukuku.Domain.Laws;

public enum BlockChangeKind
{
    /// <summary>Fıkra ya da bent metni değişti.</summary>
    Changed,

    /// <summary>Yeni fıkra ya da bent eklendi.</summary>
    Added,

    /// <summary>Fıkra ya da bent kaldırıldı (mülga).</summary>
    Removed,
}

/// <summary>
/// Bir sürümün bir önceki sürüme göre fıkra ya da bent düzeyindeki değişikliği. <see cref="ItemId"/> boşsa fıkranın
/// bentlerden önceki metni; eklenen ya da kaldırılan bütün bir fıkrada fıkranın tamamı (bentleri satır satır).
/// </summary>
/// <param name="ParagraphId">Yeni metindeki fıkra; kaldırılan fıkrada eski metindeki fıkra.</param>
/// <param name="ItemId">Bent; kaldırılan bentte eski metindeki bent.</param>
/// <param name="Label">"(2)", "(2) a)", "a)"; numarasız fıkrada boş.</param>
/// <param name="Pieces">Eski metinden yeni metne kelime düzeyinde fark.</param>
/// <param name="AnchorParagraphId">Kaldırılan fıkra/bent yeni metinde bu fıkradan (bentte: bu fıkranın içinde) sonra
/// gelirdi; null ise en başta.</param>
/// <param name="AnchorItemId">Kaldırılan bent yeni metinde bu bentten sonra gelirdi; null ise fıkranın bentlerinin başında.</param>
public sealed record BlockChange(
    Guid ParagraphId,
    Guid? ItemId,
    string Label,
    BlockChangeKind Kind,
    string OldText,
    string NewText,
    IReadOnlyList<DiffPiece> Pieces,
    Guid? AnchorParagraphId,
    Guid? AnchorItemId);

/// <summary>
/// İki sürüm metninin fıkra ve bent düzeyinde karşılaştırması. Fıkra ve bentler önce kimlikleriyle (sürüm eklenirken
/// önceki metinden taşınır, bkz. <see cref="ParagraphIdentity"/>), kimlik tutmazsa numara/etiketle eşleştirilir.
/// Sonuç yeni metnin sırasıyla; kaldırılanlar eski yerlerinde.
/// </summary>
public static class VersionComparison
{
    public static IReadOnlyList<BlockChange> Compare(IReadOnlyList<Paragraph> older, IReadOnlyList<Paragraph> newer)
    {
        var matched = Match(older, newer, p => p.Id, p => p.Number?.ToString());
        var kept = matched.Values.ToHashSet(ReferenceEqualityComparer.Instance);
        var changes = new List<BlockChange>();
        var oldIndex = 0;
        Guid? lastMatched = null;

        void EmitRemovedBefore(int index)
        {
            for (; oldIndex < index; oldIndex++)
            {
                var removed = older[oldIndex];
                if (kept.Contains(removed))
                    continue;
                var text = FullText(removed);
                changes.Add(new BlockChange(removed.Id, null, Label(removed, null), BlockChangeKind.Removed,
                    text, "", [new DiffPiece(DiffKind.Deleted, text)], lastMatched, null));
            }
        }

        foreach (var paragraph in newer)
        {
            if (!matched.TryGetValue(paragraph.Id, out var before))
            {
                var text = FullText(paragraph);
                changes.Add(new BlockChange(paragraph.Id, null, Label(paragraph, null), BlockChangeKind.Added,
                    "", text, [new DiffPiece(DiffKind.Inserted, text)], null, null));
                continue;
            }

            EmitRemovedBefore(IndexOf(older, before));
            oldIndex = Math.Max(oldIndex, IndexOf(older, before) + 1);
            lastMatched = paragraph.Id;

            if (before.Text != paragraph.Text)
                changes.Add(new BlockChange(paragraph.Id, null, Label(paragraph, null), BlockChangeKind.Changed,
                    before.Text, paragraph.Text, TextDiff.Words(before.Text, paragraph.Text), null, null));
            changes.AddRange(CompareItems(before, paragraph));
        }
        EmitRemovedBefore(older.Count);
        return changes;
    }

    private static IEnumerable<BlockChange> CompareItems(Paragraph before, Paragraph after)
    {
        var matched = Match(before.Items, after.Items, i => i.Id, i => i.Label is null ? null : $"{i.Level}{i.Label}");
        var kept = matched.Values.ToHashSet(ReferenceEqualityComparer.Instance);
        var oldIndex = 0;
        Guid? lastMatched = null;

        IEnumerable<BlockChange> RemovedBefore(int index)
        {
            for (; oldIndex < index; oldIndex++)
            {
                var removed = before.Items[oldIndex];
                if (!kept.Contains(removed))
                    yield return new BlockChange(after.Id, removed.Id, Label(after, removed), BlockChangeKind.Removed,
                        removed.Text, "", [new DiffPiece(DiffKind.Deleted, removed.Text)], after.Id, lastMatched);
            }
        }

        foreach (var item in after.Items)
        {
            if (!matched.TryGetValue(item.Id, out var old))
            {
                yield return new BlockChange(after.Id, item.Id, Label(after, item), BlockChangeKind.Added,
                    "", item.Text, [new DiffPiece(DiffKind.Inserted, item.Text)], null, null);
                continue;
            }

            var index = IndexOf(before.Items, old);
            foreach (var removed in RemovedBefore(index))
                yield return removed;
            oldIndex = Math.Max(oldIndex, index + 1);
            lastMatched = item.Id;

            if (old.Text != item.Text)
                yield return new BlockChange(after.Id, item.Id, Label(after, item), BlockChangeKind.Changed,
                    old.Text, item.Text, TextDiff.Words(old.Text, item.Text), null, null);
        }
        foreach (var removed in RemovedBefore(before.Items.Count))
            yield return removed;
    }

    /// <summary>Yeni öğenin kimliği → eşleşen eski öğe: önce kimlik, sonra numara/etiket, sonra numarasızlar sırayla.</summary>
    private static Dictionary<Guid, T> Match<T>(IReadOnlyList<T> older, IReadOnlyList<T> newer, Func<T, Guid> id, Func<T, string?> key)
        where T : class
    {
        var result = new Dictionary<Guid, T>();
        var used = new HashSet<T>(ReferenceEqualityComparer.Instance);
        var byId = older.GroupBy(id).ToDictionary(g => g.Key, g => g.First());
        foreach (var item in newer)
        {
            if (byId.TryGetValue(id(item), out var old) && used.Add(old))
                result[id(item)] = old;
        }

        var byKey = older.Where(o => !used.Contains(o) && key(o) is not null).GroupBy(o => key(o)!).ToDictionary(g => g.Key, g => g.First());
        var unkeyed = new Queue<T>(older.Where(o => !used.Contains(o) && key(o) is null));
        foreach (var item in newer.Where(n => !result.ContainsKey(id(n))))
        {
            var old = key(item) is { } k ? byKey.GetValueOrDefault(k) : unkeyed.Count > 0 ? unkeyed.Dequeue() : null;
            if (old is not null && used.Add(old))
                result[id(item)] = old;
        }
        return result;
    }

    private static int IndexOf<T>(IReadOnlyList<T> list, T item) where T : class
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], item))
                return i;
        }
        return -1;
    }

    private static string Label(Paragraph paragraph, ParagraphItem? item) =>
        string.Join(" ", new[] { paragraph.Number is { } n ? $"({n})" : null, item?.Label }.Where(s => !string.IsNullOrEmpty(s)));

    /// <summary>Fıkranın tamamı: bentlerden önceki metin ve bentler satır satır.</summary>
    public static string FullText(Paragraph paragraph) =>
        string.Join("\n", new[] { paragraph.Text }
            .Concat(paragraph.Items.Select(i => i.Label is null ? i.Text : $"{i.Label} {i.Text}"))
            .Where(line => line.Length > 0));
}
