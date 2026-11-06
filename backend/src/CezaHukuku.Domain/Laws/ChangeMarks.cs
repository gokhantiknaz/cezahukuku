namespace CezaHukuku.Domain.Laws;

public sealed record TextRange(int Start, int Length);

/// <summary>
/// Gösterilen metinde bir fıkra ya da bentin en son değişikliği (okuma ekranında kırmızı). Değişen ve eklenen
/// kelimeler <see cref="Inserted"/>; yalnız silinen yerler <see cref="DeletedAt"/> (yerine bir şey gelmeyen).
/// Kaldırılan fıkra/bent (<see cref="BlockChangeKind.Removed"/>) metinde yoktur: <see cref="AnchorParagraphId"/> /
/// <see cref="AnchorItemId"/>'den sonra "mülga" olarak gösterilir.
/// </summary>
/// <param name="VersionId">Değişikliği getiren sürüm (Değişiklikler sekmesinde o sürümün kaydı).</param>
public sealed record ChangeMark(
    Guid VersionId,
    Guid ParagraphId,
    Guid? ItemId,
    BlockChangeKind Kind,
    string Label,
    IReadOnlyList<TextRange> Inserted,
    IReadOnlyList<int> DeletedAt,
    Guid? AnchorParagraphId,
    Guid? AnchorItemId);

public static class ChangeMarks
{
    /// <summary>
    /// Gösterilen sürümün her fıkra/bentindeki en son değişiklik: sürümler eskiden yeniye sıralanır, gösterilen
    /// sürümden geriye doğru her fıkra/bent için onu son değiştiren sürüm bulunur. İlk sürümün metni değişiklik sayılmaz.
    /// </summary>
    public static IReadOnlyList<ChangeMark> For(IReadOnlyList<ArticleVersion> versions, ArticleVersion shown)
    {
        var chain = versions
            .Where(v => v.Id != shown.Id && (v.ValidFrom ?? DateOnly.MinValue) <= (shown.ValidFrom ?? DateOnly.MinValue))
            .OrderBy(v => v.ValidFrom ?? DateOnly.MinValue)
            .Append(shown)
            .ToList();

        var shownBlocks = new Dictionary<(Guid, Guid?), string>();
        foreach (var paragraph in shown.Paragraphs)
        {
            shownBlocks[(paragraph.Id, null)] = paragraph.Text;
            foreach (var item in paragraph.Items)
                shownBlocks[(paragraph.Id, item.Id)] = item.Text;
        }
        bool InShown(Guid? paragraphId, Guid? itemId) =>
            paragraphId is null || shownBlocks.ContainsKey((paragraphId.Value, itemId));

        var resolved = new HashSet<(Guid, Guid?)>();
        var marks = new List<ChangeMark>();
        for (var k = chain.Count - 1; k >= 1; k--)
        {
            var version = chain[k];
            foreach (var change in VersionComparison.Compare(chain[k - 1].Paragraphs, version.Paragraphs))
            {
                if (change.Kind == BlockChangeKind.Removed)
                {
                    var key = (change.ParagraphId, change.ItemId);
                    if (resolved.Add(key) && !shownBlocks.ContainsKey(key)
                        && (change.ItemId is null ? !shown.Paragraphs.Any(p => p.Id == change.ParagraphId) : InShown(change.ParagraphId, null))
                        && InShown(change.AnchorParagraphId, change.AnchorItemId))
                        marks.Add(new ChangeMark(version.Id, change.ParagraphId, change.ItemId, change.Kind, change.Label,
                            [], [], change.AnchorParagraphId, change.AnchorItemId));
                    continue;
                }

                // Eklenen bütün bir fıkranın her parçası ayrı işaretlenir (metni ve bentleri).
                var blocks = change is { Kind: BlockChangeKind.Added, ItemId: null }
                    ? version.Paragraphs.Where(p => p.Id == change.ParagraphId)
                        .SelectMany(p => p.Items.Select(i => ((Guid?)i.Id, i.Text)).Prepend((null, p.Text)))
                        .Select(b => (b.Item1, b.Text, Pieces: (IReadOnlyList<DiffPiece>)[new DiffPiece(DiffKind.Inserted, b.Text)]))
                    : [(change.ItemId, change.NewText, change.Pieces)];
                foreach (var (itemId, text, pieces) in blocks)
                {
                    var key = (change.ParagraphId, itemId);
                    if (text.Length == 0 || !resolved.Add(key) || shownBlocks.GetValueOrDefault(key) != text)
                        continue;
                    var (inserted, deletedAt) = Ranges(pieces);
                    if (inserted.Count + deletedAt.Count > 0)
                        marks.Add(new ChangeMark(version.Id, change.ParagraphId, itemId, change.Kind, change.Label,
                            inserted, deletedAt, null, null));
                }
            }
        }
        return marks;
    }

    /// <summary>Yeni metindeki eklenen aralıklar ve yerine bir şey gelmeyen silinmelerin konumu.</summary>
    private static (List<TextRange> Inserted, List<int> DeletedAt) Ranges(IReadOnlyList<DiffPiece> pieces)
    {
        var inserted = new List<TextRange>();
        var deletedAt = new List<int>();
        var position = 0;
        for (var i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            switch (piece.Kind)
            {
                case DiffKind.Inserted:
                    inserted.Add(new TextRange(position, piece.Text.Length));
                    position += piece.Text.Length;
                    break;
                case DiffKind.Deleted when i + 1 >= pieces.Count || pieces[i + 1].Kind != DiffKind.Inserted:
                    deletedAt.Add(position);
                    break;
                case DiffKind.Same:
                    position += piece.Text.Length;
                    break;
            }
        }
        return (inserted, deletedAt);
    }
}
