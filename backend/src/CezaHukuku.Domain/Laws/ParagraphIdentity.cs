namespace CezaHukuku.Domain.Laws;

/// <summary>
/// Metin yeniden yazılıp ayrıştırıldığında fıkra ve bent kimliklerini önceki metinden taşır; böylece metni
/// düzeltmek ya da yeni sürüm eklemek fıkraya bağlı vurgu ve notları koparmaz.
/// Eşleştirme: numaralı fıkra numarasıyla, numarasız fıkra numarasızlar arasındaki sırasıyla; bent etiketiyle
/// ("a)"), etiketsiz satır aynı seviyedeki etiketsizler arasındaki sırasıyla.
/// </summary>
public static class ParagraphIdentity
{
    public static IReadOnlyList<Paragraph> CarryOver(IReadOnlyList<Paragraph> previous, IReadOnlyList<Paragraph> next)
    {
        var byNumber = previous.Where(p => p.Number is not null)
            .GroupBy(p => p.Number!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        var unnumbered = previous.Where(p => p.Number is null).ToList();
        var used = new HashSet<Guid>();
        var unnumberedIndex = 0;

        var result = new List<Paragraph>(next.Count);
        foreach (var paragraph in next)
        {
            Paragraph? match = paragraph.Number is { } number
                ? byNumber.GetValueOrDefault(number)
                : unnumberedIndex < unnumbered.Count ? unnumbered[unnumberedIndex++] : null;

            if (match is null || !used.Add(match.Id))
            {
                result.Add(paragraph);
                continue;
            }
            result.Add(paragraph with { Id = match.Id, Items = CarryOverItems(match.Items, paragraph.Items) });
        }
        return result;
    }

    /// <summary>Kimlikler dışında aynı metin mi (fıkra numaraları, metinler, bent seviye ve etiketleri)?</summary>
    public static bool HaveSameText(IReadOnlyList<Paragraph> a, IReadOnlyList<Paragraph> b) =>
        a.Count == b.Count
        && a.Zip(b).All(pair =>
            pair.First.Number == pair.Second.Number
            && pair.First.Text == pair.Second.Text
            && pair.First.Items.Count == pair.Second.Items.Count
            && pair.First.Items.Zip(pair.Second.Items).All(items =>
                items.First.Level == items.Second.Level
                && items.First.Label == items.Second.Label
                && items.First.Text == items.Second.Text));

    private static IReadOnlyList<ParagraphItem> CarryOverItems(IReadOnlyList<ParagraphItem> previous, IReadOnlyList<ParagraphItem> next)
    {
        var byLabel = previous.Where(i => i.Label is not null)
            .GroupBy(i => (i.Level, i.Label))
            .ToDictionary(g => g.Key, g => g.First());
        var unlabeled = previous.Where(i => i.Label is null).ToLookup(i => i.Level);
        var unlabeledIndex = new Dictionary<int, int>();
        var used = new HashSet<Guid>();

        var result = new List<ParagraphItem>(next.Count);
        foreach (var item in next)
        {
            ParagraphItem? match;
            if (item.Label is not null)
            {
                match = byLabel.GetValueOrDefault((item.Level, item.Label));
            }
            else
            {
                var index = unlabeledIndex.GetValueOrDefault(item.Level);
                unlabeledIndex[item.Level] = index + 1;
                match = unlabeled[item.Level].ElementAtOrDefault(index);
            }

            result.Add(match is not null && used.Add(match.Id) ? item with { Id = match.Id } : item);
        }
        return result;
    }
}
