using CezaHukuku.Infrastructure.Content;

namespace CezaHukuku.UnitTests.Content;

public class RichTextTests
{
    [Fact]
    public void KeepsFormattingFromEditor()
    {
        const string html = "<h2>Gerekçe</h2><p>Madde ile <strong>kasten</strong> öldürme <em>düzenlenmiştir</em>.</p>"
            + "<ul><li>bir</li><li>iki</li></ul><blockquote><p>alıntı</p></blockquote>";

        Assert.Equal(html, RichText.Sanitize(html));
    }

    [Theory]
    [InlineData("<p>a<script>alert(1)</script></p>", "<p>a</p>")]
    [InlineData("<p onclick=\"alert(1)\">a</p>", "<p>a</p>")]
    [InlineData("<p style=\"color:red\">a</p>", "<p>a</p>")]
    [InlineData("<img src=x onerror=alert(1)><p>a</p>", "<p>a</p>")]
    [InlineData("<iframe src=\"https://kotu.example\"></iframe><p>a</p>", "<p>a</p>")]
    [InlineData("<div><span>a</span></div>", "a")]
    public void RemovesScriptsHandlersStylesAndUnknownTags(string input, string expected)
    {
        Assert.Equal(expected, RichText.Sanitize(input));
    }

    [Fact]
    public void Links_DropJavascriptAndOpenSafelyInNewTab()
    {
        Assert.Equal("<p><a>tıkla</a></p>", RichText.Sanitize("<p><a href=\"javascript:alert(1)\">tıkla</a></p>"));
        Assert.Equal(
            "<p><a href=\"https://www.mevzuat.gov.tr/\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">mevzuat</a></p>",
            RichText.Sanitize("<p><a href=\"https://www.mevzuat.gov.tr/\" target=\"_self\">mevzuat</a></p>"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p></p>")]
    [InlineData("<p> <br> </p>")]
    [InlineData("<script>alert(1)</script>")]
    public void EmptyOrOnlyRemovedContent_BecomesEmpty(string? input)
    {
        Assert.Equal("", RichText.Sanitize(input));
    }

    [Fact]
    public void DropsEmptyParagraphsLeftByEditor()
    {
        Assert.Equal("<p>Metin.</p><ul><li><p>bir</p></li></ul>",
            RichText.Sanitize("<p>Metin.</p><p></p><ul><li><p>bir</p></li></ul><p><br></p>"));
    }

    [Fact]
    public void ToPlainText_SeparatesBlocks()
    {
        Assert.Equal("Başlık Birinci paragraf. madde bir madde iki",
            RichText.ToPlainText("<h2>Başlık</h2><p>Birinci paragraf.</p><ul><li>madde bir</li><li>madde iki</li></ul>"));
    }
}
