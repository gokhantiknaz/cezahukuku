using CezaHukuku.Domain.Laws;

namespace CezaHukuku.UnitTests.Laws;

public class ParagraphIdentityTests
{
    [Fact]
    public void CarryOver_MatchesNumberedParagraphsByNumber()
    {
        var previous = new[] { P(1, "Eski bir."), P(2, "Eski iki.") };
        var next = new[] { P(2, "Yeni iki."), P(3, "Yeni üç.") };

        var result = ParagraphIdentity.CarryOver(previous, next);

        Assert.Equal(previous[1].Id, result[0].Id);
        Assert.Equal(next[1].Id, result[1].Id);
        Assert.Equal("Yeni iki.", result[0].Text);
    }

    [Fact]
    public void CarryOver_MatchesUnnumberedParagraphsByOrder()
    {
        var previous = new[] { P(null, "A"), P(null, "B") };
        var next = new[] { P(null, "A'"), P(null, "B'"), P(null, "C") };

        var result = ParagraphIdentity.CarryOver(previous, next);

        Assert.Equal([previous[0].Id, previous[1].Id, next[2].Id], result.Select(p => p.Id));
    }

    [Fact]
    public void CarryOver_MatchesClausesByLabelAndClosingTextByOrder()
    {
        var previous = new[] { P(1, "Giriş;", I(1, "a)"), I(1, "b)"), I(0, null)) };
        var next = new[] { P(1, "Giriş;", I(1, "b)"), I(1, "c)"), I(0, null)) };

        var items = ParagraphIdentity.CarryOver(previous, next)[0].Items;

        Assert.Equal(previous[0].Items[1].Id, items[0].Id);
        Assert.Equal(next[0].Items[1].Id, items[1].Id);
        Assert.Equal(previous[0].Items[2].Id, items[2].Id);
    }

    [Fact]
    public void CarryOver_DoesNotReuseOneIdTwice()
    {
        var previous = new[] { P(1, "Bir.") };
        var next = new[] { P(1, "Bir."), P(1, "Yanlışlıkla yine bir.") };

        var result = ParagraphIdentity.CarryOver(previous, next);

        Assert.Equal(previous[0].Id, result[0].Id);
        Assert.NotEqual(result[0].Id, result[1].Id);
    }

    private static Paragraph P(int? number, string text, params ParagraphItem[] items) =>
        new(Guid.CreateVersion7(), number, text, items);

    private static ParagraphItem I(int level, string? label) => new(Guid.CreateVersion7(), level, label, "metin");
}
