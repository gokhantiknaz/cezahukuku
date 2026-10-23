using CezaHukuku.Parsing.Legislation;

namespace CezaHukuku.UnitTests.Parsing;

public class ArticleReferenceParserTests
{
    [Theory]
    [InlineData("tck 81", "tck", "81")]
    [InlineData("TCK 81", "tck", "81")]
    [InlineData("TCK m.81", "tck", "81")]
    [InlineData("tck md. 81", "tck", "81")]
    [InlineData("TCK madde 81", "tck", "81")]
    [InlineData("5237/81", "5237", "81")]
    [InlineData("765 448", "765", "448")]
    [InlineData("tck 103/a", "tck", "103/A")]
    [InlineData("CMK geçici 1", "cmk", "Geçici 1")]
    [InlineData("cmk gecici madde 2", "cmk", "Geçici 2")]
    [InlineData("CGTİK 5", "cgtik", "5")]
    public void Parse_ReadsLawAndArticle(string text, string law, string number)
    {
        Assert.Equal(new ArticleReference(law, number), ArticleReferenceParser.Parse(text));
    }

    [Theory]
    [InlineData("kasten öldürme")]
    [InlineData("81")]
    [InlineData("tck")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_ReturnsNullForFreeText(string? text)
    {
        Assert.Null(ArticleReferenceParser.Parse(text));
    }
}
