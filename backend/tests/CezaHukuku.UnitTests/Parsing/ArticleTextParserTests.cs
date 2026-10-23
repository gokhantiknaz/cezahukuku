using CezaHukuku.Parsing.Legislation;

namespace CezaHukuku.UnitTests.Parsing;

public class ArticleTextParserTests
{
    [Fact]
    public void Parse_SplitsNumberedParagraphs()
    {
        var result = ArticleTextParser.Parse("(1) Birinci fıkra.\n(2) İkinci fıkra.");

        Assert.Equal([1, 2], result.Select(p => p.Number));
        Assert.Equal(["Birinci fıkra.", "İkinci fıkra."], result.Select(p => p.Text));
    }

    [Fact]
    public void Parse_ReadsClausesAndClosingText()
    {
        const string text = """
            (1) Kasten öldürme suçunun;
            a) Tasarlayarak,
            b) Canavarca hisle veya eziyet çektirerek,
            ç) Üstsoy veya altsoya karşı,
            İşlenmesi hâlinde, kişi ağırlaştırılmış müebbet hapis cezası ile cezalandırılır.
            """;

        var paragraph = Assert.Single(ArticleTextParser.Parse(text));

        Assert.Equal("Kasten öldürme suçunun;", paragraph.Text);
        Assert.Equal(["a)", "b)", "ç)", null], paragraph.Items.Select(i => i.Label));
        Assert.Equal([1, 1, 1, 0], paragraph.Items.Select(i => i.Level));
        Assert.StartsWith("İşlenmesi hâlinde", paragraph.Items[^1].Text);
    }

    [Fact]
    public void Parse_NumberedLinesAreSubClausesUnderLetteredClauses()
    {
        var paragraph = Assert.Single(ArticleTextParser.Parse("(1) Giriş:\na) Bent\n1. Alt bent\n2) Diğer alt bent"));

        Assert.Equal([1, 2, 2], paragraph.Items.Select(i => i.Level));
        Assert.Equal(["a)", "1.", "2)"], paragraph.Items.Select(i => i.Label));
    }

    [Fact]
    public void Parse_NumberedLinesWithoutLetteredClausesAreClauses()
    {
        var paragraph = Assert.Single(ArticleTextParser.Parse("(1) Şunlar:\n1. Birinci\n2. İkinci"));

        Assert.Equal([1, 1], paragraph.Items.Select(i => i.Level));
    }

    [Fact]
    public void Parse_PlainLinesWithoutClausesAreUnnumberedParagraphs()
    {
        var result = ArticleTextParser.Parse("Bu Kanunun amacı...\n\n  Bu Kanun   yayımı tarihinde yürürlüğe girer.  ");

        Assert.Equal([null, null], result.Select(p => p.Number));
        Assert.Equal("Bu Kanun yayımı tarihinde yürürlüğe girer.", result[1].Text);
    }

    [Theory]
    [InlineData("MADDE 81 – (1) Metin.")]
    [InlineData("Madde 103/A - (1) Metin.")]
    [InlineData("GEÇİCİ MADDE 2 — (1) Metin.")]
    public void Parse_DropsLeadingArticleHeader(string text)
    {
        var paragraph = Assert.Single(ArticleTextParser.Parse(text));

        Assert.Equal(1, paragraph.Number);
        Assert.Equal("Metin.", paragraph.Text);
    }

    [Fact]
    public void Parse_HandlesWindowsLineEndingsAndBlankInput()
    {
        Assert.Equal(2, ArticleTextParser.Parse("(1) A\r\n(2) B\r\n").Count);
        Assert.Empty(ArticleTextParser.Parse("  \n \r\n"));
        Assert.Empty(ArticleTextParser.Parse(null));
    }
}
