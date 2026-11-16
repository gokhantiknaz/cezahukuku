using CezaHukuku.Infrastructure.Content;

namespace CezaHukuku.UnitTests.Content;

/// <summary>Spec 0005: ders notunun okuma HTML'inde parantez içi atıfların işaretlenmesi.</summary>
public class ArticleReferenceHtmlTests
{
    private static ArticleReferenceTarget? Tck(string number) =>
        number is "21" or "22" or "53" ? new ArticleReferenceTarget("tck", $"madde-{number}", "TCK", number) : null;

    [Fact]
    public void Wraps_TextNodes_SkipsExistingLinks_KeepsText()
    {
        const string html = "<p>Kast <strong>(madde 21, 22)</strong> ve <a href=\"https://ornek.org\">(madde 53)</a> (madde 999).</p>";

        var marked = ArticleReferenceHtml.Mark(html, Tck);

        Assert.Contains(
            "<strong>(madde <a href=\"/tck/madde-21\" class=\"article-ref\" data-article-ref=\"tck/madde-21\" aria-label=\"TCK madde 21'i aç\">21</a>, "
            + "<a href=\"/tck/madde-22\" class=\"article-ref\" data-article-ref=\"tck/madde-22\" aria-label=\"TCK madde 22'yi aç\">22</a>)</strong>",
            marked);
        // Var olan bağlantının içi ve çözülemeyen numara düz kalır.
        Assert.Contains("<a href=\"https://ornek.org\">(madde 53)</a>", marked);
        Assert.Contains("(madde 999)", marked);
        Assert.Equal(2, marked.Split("article-ref\"").Length - 1);
        Assert.Equal(RichText.ToPlainText(html), RichText.ToPlainText(marked));
    }

    [Fact]
    public void NothingToMark_ReturnsHtmlUnchanged()
    {
        const string html = "<p>Atıf yok &amp; (madde 999).</p>";

        Assert.Same(html, ArticleReferenceHtml.Mark(html, Tck));
    }

    [Fact]
    public void ParagraphSuffix_IsInsideLink()
    {
        var marked = ArticleReferenceHtml.Mark("<p>(madde 53/1-c)</p>", Tck);

        Assert.Contains("data-article-ref=\"tck/madde-53\" aria-label=\"TCK madde 53'ü aç\">53/1-c</a>", marked);
    }

    [Theory]
    [InlineData("1", "1'i")]
    [InlineData("2", "2'yi")]
    [InlineData("3", "3'ü")]
    [InlineData("6", "6'yı")]
    [InlineData("9", "9'u")]
    [InlineData("10", "10'u")]
    [InlineData("20", "20'yi")]
    [InlineData("40", "40'ı")]
    [InlineData("70", "70'i")]
    [InlineData("100", "100'ü")]
    [InlineData("231", "231'i")]
    public void AccessibleName_UsesAccusativeSuffix(string number, string expected)
    {
        Assert.Equal($"TCK madde {expected} aç", ArticleReferenceHtml.AccessibleName("TCK", number));
    }
}
