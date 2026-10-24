using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;

namespace CezaHukuku.UnitTests.Personal;

public class TextAnchoringTests
{
    private const string First = "Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.";
    private const string Second = "Kişi, bu suçu taksirle işlerse iki yıldan altı yıla kadar hapis cezası ile cezalandırılır.";

    [Fact]
    public void Create_StoresQuoteAndSurroundingText()
    {
        var paragraphs = new[] { P(1, First) };
        var start = First.IndexOf("müebbet", StringComparison.Ordinal);

        var anchor = TextAnchoring.Create(paragraphs, new TextPosition(paragraphs[0].Id, null, start, "müebbet hapis".Length))!;

        Assert.Equal("müebbet hapis", anchor.Quote);
        Assert.Equal(First[(start - TextAnchoring.ContextLength)..start], anchor.Prefix);
        Assert.Equal(" cezası ile cezalandırılır.", anchor.Suffix);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(0, 0)]
    [InlineData(70, 10)]
    public void Create_RejectsPositionOutsideText(int start, int length)
    {
        var paragraphs = new[] { P(1, First) };
        Assert.Null(TextAnchoring.Create(paragraphs, new TextPosition(paragraphs[0].Id, null, start, length)));
    }

    [Fact]
    public void Create_RejectsUnknownBlockAndWhitespace()
    {
        var paragraphs = new[] { P(1, First) };
        Assert.Null(TextAnchoring.Create(paragraphs, new TextPosition(Guid.NewGuid(), null, 0, 3)));
        Assert.Null(TextAnchoring.Create(paragraphs, new TextPosition(paragraphs[0].Id, null, First.IndexOf(' '), 1)));
    }

    [Fact]
    public void Create_AnchorsToClauseText()
    {
        var paragraph = P(1, "Giriş:", I("a) Üstsoy veya altsoyuna,"));
        var item = paragraph.Items[0];

        var anchor = TextAnchoring.Create([paragraph], new TextPosition(paragraph.Id, item.Id, 3, 7))!;

        Assert.Equal((item.Id, "Üstsoy"), (anchor.ItemId, anchor.Quote[..6]));
    }

    [Fact]
    public void Locate_SameText_ReturnsStoredPosition()
    {
        var paragraphs = new[] { P(1, First), P(2, Second) };
        var anchor = AnchorOn(paragraphs, 1, "hapis cezası");

        var position = TextAnchoring.Locate(anchor, paragraphs);

        Assert.Equal(new TextPosition(paragraphs[1].Id, null, anchor.Start, anchor.Length), position);
    }

    [Fact]
    public void Locate_TextChangedBeforeQuote_FindsQuoteInSameParagraph()
    {
        var paragraphs = new[] { P(1, First) };
        var anchor = AnchorOn(paragraphs, 0, "müebbet hapis cezası");
        var amended = new[] { paragraphs[0] with { Text = "Bir insanı kasten ve tasarlayarak öldüren kişi, müebbet hapis cezası ile cezalandırılır." } };

        var position = TextAnchoring.Locate(anchor, amended)!;

        Assert.Equal("müebbet hapis cezası", amended[0].Text.Substring(position.Start, position.Length));
    }

    [Fact]
    public void Locate_RepeatedQuote_PicksOccurrenceWithMatchingContext()
    {
        // "hapis cezası" iki fıkrada da geçiyor; vurgu ikincisindeydi ve ikinci fıkra yeni sürümde birinciye taşındı.
        var paragraphs = new[] { P(1, First), P(2, Second) };
        var anchor = AnchorOn(paragraphs, 1, "hapis cezası");
        var renumbered = new[] { P(1, Second), P(2, First) };

        var position = TextAnchoring.Locate(anchor, renumbered)!;

        Assert.Equal(renumbered[0].Id, position.ParagraphId);
        Assert.Equal(Second.IndexOf("hapis cezası", StringComparison.Ordinal), position.Start);
    }

    [Fact]
    public void Locate_ShortQuoteWithoutContextElsewhere_IsNotRelocated()
    {
        var paragraphs = new[] { P(1, "Kişi cezalandırılır.") };
        var anchor = AnchorOn(paragraphs, 0, "Kişi");
        var rewritten = new[] { P(1, "Tamamen başka bir metin."), P(2, "Kişi") };

        Assert.Null(TextAnchoring.Locate(anchor, rewritten));
    }

    [Fact]
    public void Locate_QuoteRemoved_ReturnsNull()
    {
        var paragraphs = new[] { P(1, First) };
        var anchor = AnchorOn(paragraphs, 0, "müebbet");
        var amended = new[] { paragraphs[0] with { Text = "Bir insanı kasten öldüren kişi, ağırlaştırılmış hapis cezası ile cezalandırılır." } };

        Assert.Null(TextAnchoring.Locate(anchor, amended));
    }

    private static TextAnchor AnchorOn(IReadOnlyList<Paragraph> paragraphs, int index, string quote)
    {
        var text = paragraphs[index].Text;
        return TextAnchoring.Create(paragraphs,
            new TextPosition(paragraphs[index].Id, null, text.IndexOf(quote, StringComparison.Ordinal), quote.Length))!;
    }

    private static Paragraph P(int? number, string text, params ParagraphItem[] items) =>
        new(Guid.NewGuid(), number, text, items);

    private static ParagraphItem I(string text) => new(Guid.NewGuid(), 1, null, text);
}
