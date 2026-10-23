using CezaHukuku.Parsing.Text;

namespace CezaHukuku.UnitTests.Parsing;

public class TurkishTextTests
{
    [Theory]
    [InlineData("İHMAL", "ihmal")]
    [InlineData("KASTEN ÖLDÜRME", "kasten öldürme")]
    [InlineData("ISPAT", "ıspat")]
    [InlineData("  Kasten   öldürme\t\n ", "kasten öldürme")]
    public void Normalize_UsesTurkishCasingAndCollapsesWhitespace(string input, string expected)
    {
        Assert.Equal(expected, TurkishText.Normalize(input));
    }

    [Theory]
    [InlineData("Öldürme", "oldurme")]
    [InlineData("Çocuğun düşürülmesi", "cocugun dusurulmesi")]
    [InlineData("İHMALİ DAVRANIŞ", "ihmali davranis")]
    public void Fold_RemovesTurkishDiacritics(string input, string expected)
    {
        Assert.Equal(expected, TurkishText.Fold(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ReturnsEmptyForBlankInput(string? input)
    {
        Assert.Equal(string.Empty, TurkishText.Normalize(input));
    }
}
