using CezaHukuku.Domain.Laws;

namespace CezaHukuku.UnitTests.Laws;

public class ArticleVersionTests
{
    private static readonly ArticleVersion First = Version(new(2005, 6, 1), new(2014, 7, 1));
    private static readonly ArticleVersion Current = Version(new(2014, 7, 2), null);

    [Fact]
    public void Select_WithoutDate_ReturnsVersionInForce()
    {
        Assert.Same(Current, ArticleVersion.Select([First, Current], asOf: null));
    }

    [Theory]
    [InlineData(2005, 6, 1, 0)]
    [InlineData(2014, 7, 1, 0)]
    [InlineData(2014, 7, 2, 1)]
    [InlineData(2030, 1, 1, 1)]
    public void Select_WithDate_ReturnsVersionInForceOnThatDay(int year, int month, int day, int expectedIndex)
    {
        ArticleVersion[] versions = [First, Current];

        Assert.Same(versions[expectedIndex], ArticleVersion.Select(versions, new DateOnly(year, month, day)));
    }

    [Fact]
    public void Select_BeforeFirstVersion_ReturnsNull()
    {
        Assert.Null(ArticleVersion.Select([First, Current], new DateOnly(2004, 1, 1)));
    }

    [Fact]
    public void Select_WhenNoVersionInForce_ReturnsLatest()
    {
        var repealed = Version(new(2016, 1, 1), new(2020, 1, 1));

        Assert.Same(repealed, ArticleVersion.Select([First, repealed], asOf: null));
    }

    [Fact]
    public void Overlaps_TreatsOpenEndsAsUnbounded()
    {
        Assert.False(First.Overlaps(Current));
        Assert.True(Version(new(2014, 7, 1), null).Overlaps(First));
        Assert.True(Version(null, null).Overlaps(Current));
    }

    private static ArticleVersion Version(DateOnly? from, DateOnly? to) => new() { ValidFrom = from, ValidTo = to };
}
