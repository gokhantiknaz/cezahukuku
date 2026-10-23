using CezaHukuku.Parsing.Text;

namespace CezaHukuku.UnitTests.Parsing;

public class SlugsTests
{
    [Theory]
    [InlineData("TCK", "tck")]
    [InlineData("CGTİK", "cgtik")]
    [InlineData("Temel Ceza Kanunları", "temel-ceza-kanunlari")]
    [InlineData("  Özel / Ceza  ", "ozel-ceza")]
    [InlineData("5237", "5237")]
    public void FromText_FoldsAndHyphenates(string input, string expected)
    {
        Assert.Equal(expected, Slugs.FromText(input));
    }

    [Theory]
    [InlineData("81", "madde-81")]
    [InlineData("103/A", "madde-103-a")]
    [InlineData("Madde 81", "madde-81")]
    [InlineData("Ek 1", "ek-madde-1")]
    [InlineData("Ek Madde 1", "ek-madde-1")]
    [InlineData("Geçici 2", "gecici-madde-2")]
    [InlineData("GEÇİCİ MADDE 2", "gecici-madde-2")]
    public void ForArticle_BuildsStableArticleSlug(string number, string expected)
    {
        Assert.Equal(expected, Slugs.ForArticle(number));
    }

    [Theory]
    [InlineData("tck", true)]
    [InlineData("madde-103-a", true)]
    [InlineData("TCK", false)]
    [InlineData("-tck", false)]
    [InlineData("tck--81", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_AcceptsOnlyLowercaseHyphenated(string? slug, bool expected)
    {
        Assert.Equal(expected, Slugs.IsValid(slug));
    }
}
