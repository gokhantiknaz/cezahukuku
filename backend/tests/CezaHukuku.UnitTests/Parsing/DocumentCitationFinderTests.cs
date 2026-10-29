using CezaHukuku.Parsing.Legislation;

namespace CezaHukuku.UnitTests.Parsing;

public class DocumentCitationFinderTests
{
    [Theory]
    [InlineData("Sanık hakkında TCK 81 uygulanmıştır.", "TCK 81", "81")]
    [InlineData("TCK m. 82 kapsamında", "TCK m. 82", "82")]
    [InlineData("TCK md.83'e göre", "TCK md.83", "83")]
    [InlineData("TCK'nın 81. maddesi uyarınca", "TCK'nın 81. maddesi", "81")]
    [InlineData("CMK’nun 231 inci maddesi", "CMK’nun 231 inci maddesi", "231")]
    [InlineData("TCK madde 103/a", "TCK madde 103/a", "103/A")]
    public void ShortForms(string text, string expectedMatch, string article)
    {
        var citation = Assert.Single(DocumentCitationFinder.Find(text));

        Assert.Equal(DocumentLawKind.Abbreviation, citation.Kind);
        Assert.Equal(expectedMatch, text.Substring(citation.Start, citation.Length));
        Assert.Equal(article, citation.ArticleNumber);
    }

    [Fact]
    public void NumberedLaw_FromLegislationParser()
    {
        var citation = Assert.Single(DocumentCitationFinder.Find("5271 sayılı Ceza Muhakemesi Kanununun 231 inci maddesi uyarınca"));

        Assert.Equal((DocumentLawKind.Number, "5271", "231"), (citation.Kind, citation.LawValue, citation.ArticleNumber));
    }

    [Theory]
    [InlineData("AYM 2019/123 E. sayılı kararı")] // esas numarası
    [InlineData("Daire 13 üncü maddede")] // kanunu belli değil
    [InlineData("tck 81")] // küçük harf kısaltma (sıradan sözcük olabilir)
    [InlineData("YTCK12345")] // bitişik uzun numara
    public void NotCitations(string text) =>
        Assert.Empty(DocumentCitationFinder.Find(text));

    [Theory]
    [InlineData("TCK 81/2 uyarınca", "TCK 81/2", "81", 2, null)]
    [InlineData("TCK m. 53/1-c bendi", "TCK m. 53/1-c", "53", 1, "c")]
    [InlineData("CMK 231/5'e göre", "CMK 231/5", "231", 5, null)]
    [InlineData("TCK madde 103/a", "TCK madde 103/a", "103/A", null, null)]
    [InlineData("TCK'nın 82/1-a maddesi kapsamında", "TCK'nın 82/1-a maddesi", "82", 1, "a")]
    public void ParagraphAndItem(string text, string expectedMatch, string article, int? paragraph, string? item)
    {
        var citation = Assert.Single(DocumentCitationFinder.Find(text));

        Assert.Equal(expectedMatch, text.Substring(citation.Start, citation.Length));
        Assert.Equal((article, paragraph, item), (citation.ArticleNumber, citation.Paragraph, citation.Item));
    }

    [Fact]
    public void YearAndCaseNumber_IsNotParagraph() =>
        Assert.Empty(DocumentCitationFinder.Find("AYM 2019/12 sayılı karar"));

    [Fact]
    public void SeveralInOneSentence()
    {
        var citations = DocumentCitationFinder.Find("TCK 81 ve TCK m. 82 ile CMK 231 birlikte");

        Assert.Equal(["81", "82", "231"], citations.Select(c => c.ArticleNumber));
    }
}
