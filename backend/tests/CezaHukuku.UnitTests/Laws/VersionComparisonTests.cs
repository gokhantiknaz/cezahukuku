using CezaHukuku.Domain.Laws;

namespace CezaHukuku.UnitTests.Laws;

public class VersionComparisonTests
{
    [Fact]
    public void Words_GroupsAdjacentChangedWords_DeletedBeforeInserted()
    {
        var pieces = TextDiff.Words("altı aydan üç yıla kadar hapis", "bir yıldan beş yıla kadar hapis");

        Assert.Equal(
            [(DiffKind.Deleted, "altı aydan üç"), (DiffKind.Inserted, "bir yıldan beş"), (DiffKind.Same, " yıla kadar hapis")],
            pieces.Select(p => (p.Kind, p.Text)));
    }

    [Fact]
    public void Words_PureInsertionAndDeletion()
    {
        Assert.Equal(
            [(DiffKind.Same, "Kişi "), (DiffKind.Inserted, "kasten "), (DiffKind.Same, "öldürür.")],
            TextDiff.Words("Kişi öldürür.", "Kişi kasten öldürür.").Select(p => (p.Kind, p.Text)));
        Assert.Equal(
            [(DiffKind.Same, "vergiler ile "), (DiffKind.Deleted, "diğer "), (DiffKind.Same, "malî yükümlülükleri")],
            TextDiff.Words("vergiler ile diğer malî yükümlülükleri", "vergiler ile malî yükümlülükleri").Select(p => (p.Kind, p.Text)));
    }

    [Fact]
    public void Compare_MatchesByIdThenNumber_AndReportsClauseLevelChanges()
    {
        var older = new[]
        {
            P(null, "Bu Kanunda yer alan;",
                I("a)", "Gümrük vergileri: Gümrük idaresi veya başka idarelerce uygulanan vergiler ile diğer malî yükümlülükleri,"),
                I("b)", "Eşya: Her türlü madde,"),
                I("c)", "Kaldırılacak bent,")),
            P(2, "İkinci fıkra."),
        };
        var newer = new[]
        {
            // Kimlik taşınmamış (ayrı girilmiş metin): fıkra sırayla, bentler etiketle eşleşir.
            P(null, "Bu Kanunda yer alan;",
                I("a)", "Gümrük vergileri: Gümrük idaresince uygulanan vergiler ile malî yükümlülükleri,"),
                I("b)", "Eşya: Her türlü madde,"),
                I("ç)", "Yeni bent,")),
            P(3, "Yeni fıkra."),
        };

        var changes = VersionComparison.Compare(older, newer);

        Assert.Equal(
            [
                ("a)", BlockChangeKind.Changed),
                ("ç)", BlockChangeKind.Added),
                ("c)", BlockChangeKind.Removed),
                ("(3)", BlockChangeKind.Added),
                ("(2)", BlockChangeKind.Removed),
            ],
            changes.Select(c => (c.Label, c.Kind)));
        var changed = changes[0];
        Assert.Equal(newer[0].Items[0].Id, changed.ItemId);
        Assert.Equal(
            [(DiffKind.Deleted, "idaresi veya başka idarelerce"), (DiffKind.Inserted, "idaresince")],
            changed.Pieces.Where(p => p.Kind != DiffKind.Same).Select(p => (p.Kind, p.Text)).Take(2));
        // Kaldırılan bent b)'den, kaldırılan fıkra ilk fıkradan sonra gelirdi.
        Assert.Equal((newer[0].Id, newer[0].Items[1].Id), (changes[2].AnchorParagraphId, changes[2].AnchorItemId));
        Assert.Equal(newer[0].Id, changes[4].AnchorParagraphId);
        Assert.Equal("İkinci fıkra.", changes[4].OldText);
    }

    [Fact]
    public void ChangeMarks_ShowEachBlocksLatestChange_InShownText()
    {
        var first = Version(2005, P(1, "Bir insanı öldüren kişi, müebbet hapis cezası ile cezalandırılır."), P(2, "İkinci fıkra."));
        var second = Version(2010,
            first.Paragraphs[0] with { Text = "Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır." },
            first.Paragraphs[1]);
        var third = Version(2016, second.Paragraphs[0]);
        var fourth = Version(2020,
            second.Paragraphs[0] with { Text = "Bir insanı kasten öldüren kişi, hapis cezası ile cezalandırılır." },
            P(3, "Üçüncü fıkra."));

        var marks = ChangeMarks.For([first, second, third, fourth], fourth);

        var changed = Assert.Single(marks, m => m.Kind == BlockChangeKind.Changed);
        Assert.Equal(fourth.Id, changed.VersionId);
        Assert.Empty(changed.Inserted);
        Assert.Equal(["Bir insanı kasten öldüren kişi, ".Length], changed.DeletedAt);
        var added = Assert.Single(marks, m => m.Kind == BlockChangeKind.Added);
        Assert.Equal([new TextRange(0, "Üçüncü fıkra.".Length)], added.Inserted);
        // (2) 2016'da kaldırıldı: (1)'den sonra "mülga".
        var removed = Assert.Single(marks, m => m.Kind == BlockChangeKind.Removed);
        Assert.Equal((third.Id, "(2)", first.Paragraphs[0].Id), (removed.VersionId, removed.Label, removed.AnchorParagraphId));

        // 2010'daki metin gösterilirken: "kasten" eklenmiş.
        var then = Assert.Single(ChangeMarks.For([first, second, third, fourth], second));
        Assert.Equal([new TextRange("Bir insanı ".Length, "kasten ".Length)], then.Inserted);
        Assert.Empty(ChangeMarks.For([first, second], first));
    }

    private static ArticleVersion Version(int year, params Paragraph[] paragraphs) =>
        new() { ValidFrom = new DateOnly(year, 1, 1), Paragraphs = paragraphs };

    private static Paragraph P(int? number, string text, params ParagraphItem[] items) => new(Guid.NewGuid(), number, text, items);

    private static ParagraphItem I(string label, string text) => new(Guid.NewGuid(), 1, label, text);
}
