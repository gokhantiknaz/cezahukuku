using CezaHukuku.Parsing.Legislation;

namespace CezaHukuku.UnitTests.Parsing;

/// <summary>Spec 0005, R-2 / AC-2: parantez içinde, kanun adı geçmeden yazılmış madde atıfları.</summary>
public class ParenthesizedArticleFinderTests
{
    private static (string Marked, string Number)[] Found(string text) =>
        [.. ParenthesizedArticleFinder.Find(text).Select(r => (text.Substring(r.Start, r.Length), r.ArticleNumber))];

    [Theory]
    [InlineData("Kast (madde 21) suçun manevi unsurudur.", new[] { "21" })]
    [InlineData("Kast ve taksir (Madde 21 ve 22) birlikte.", new[] { "21", "22" })]
    [InlineData("Kast ve taksir (madde 21, 22) birlikte.", new[] { "21", "22" })]
    [InlineData("Kast ve taksir (maddeler 21, 22) birlikte.", new[] { "21", "22" })]
    [InlineData("Kast (m. 21) suçun manevi unsurudur.", new[] { "21" })]
    [InlineData("Kast (md.21) suçun manevi unsurudur.", new[] { "21" })]
    [InlineData("Kast (MADDE 21) suçun manevi unsurudur.", new[] { "21" })]
    [InlineData("Olası kast ( madde  21 ,22 ve 23 ) birlikte.", new[] { "21", "22", "23" })]
    public void Finds_RecognizedForms(string text, string[] numbers)
    {
        Assert.Equal(numbers, Found(text).Select(f => f.Number));
        // İşaretlenen parça numaranın kendisidir: metin aynen kalır, yalnız numara bağlanır.
        Assert.Equal(numbers, Found(text).Select(f => f.Marked));
    }

    [Fact]
    public void ParagraphSuffix_IsMarkedWithNumber_ButArticleNumberIsPlain()
    {
        Assert.Equal([("21/2", "21"), ("53/1-c", "53")], Found("Bkz. (madde 21/2, 53/1-c)."));
    }

    [Fact]
    public void EachNumberIsFoundAtItsOwnPosition()
    {
        const string text = "(madde 21) ve (madde 21, 22)";

        var found = ParenthesizedArticleFinder.Find(text);

        Assert.Equal([7, 21, 25], found.Select(f => f.Start));
    }

    [Theory]
    [InlineData("Madde 21'e göre kast aranır.")]
    [InlineData("madde 21 uyarınca")]
    [InlineData("(madde 21-23)")]
    [InlineData("(madde 21 ila 23)")]
    [InlineData("(TCK m. 21)")]
    [InlineData("(TCK madde 21)")]
    [InlineData("(madde 21, TCK)")]
    [InlineData("(geçici madde 1)")]
    [InlineData("(Ek madde 2)")]
    [InlineData("(madde)")]
    [InlineData("(21)")]
    [InlineData("(5 madde hâlinde)")]
    [InlineData("(madde 1234)")]
    [InlineData("(maddenin 21 inci fıkrası)")]
    [InlineData("")]
    public void Ignores_UnrecognizedForms(string text)
    {
        Assert.Empty(ParenthesizedArticleFinder.Find(text));
    }

    [Fact]
    public void InnermostParentheses_AreRead()
    {
        Assert.Equal([("21", "21")], Found("Kast (bkz. (madde 21)) aranır."));
    }
}
