using CezaHukuku.Parsing.Search;

namespace CezaHukuku.UnitTests.Parsing;

public class SearchQueryParserTests
{
    [Fact]
    public void Words_AreJoinedWithAnd_AndNormalizedTurkish()
    {
        var node = SearchQueryParser.Parse("İHMAL Öldürme");

        var and = Assert.IsType<SearchAnd>(node);
        Assert.Equal(new SearchTerm("ihmal", "ihmal", null), and.Left);
        Assert.Equal(new SearchTerm("öldürme", "oldurme", "oldur"), and.Right);
    }

    [Theory]
    [InlineData("hırsızlık VEYA yağma")]
    [InlineData("hırsızlık OR yağma")]
    [InlineData("hırsızlık | yağma")]
    public void Or_Operators(string query) =>
        Assert.IsType<SearchOr>(SearchQueryParser.Parse(query));

    [Fact]
    public void Precedence_NotThenAndThenOr_WithParentheses()
    {
        // ("müebbet" VE "ihmal") -taksir
        var node = SearchQueryParser.Parse("(\"müebbet\" VE \"ihmal\") -taksir");

        var and = Assert.IsType<SearchAnd>(node);
        var inner = Assert.IsType<SearchAnd>(and.Left);
        Assert.Equal("müebbet", Assert.IsType<SearchTerm>(inner.Left).Normalized);
        Assert.Equal("taksir", Assert.IsType<SearchTerm>(Assert.IsType<SearchNot>(and.Right).Inner).Normalized);
    }

    [Fact]
    public void AndBindsTighterThanOr()
    {
        var or = Assert.IsType<SearchOr>(SearchQueryParser.Parse("a1 b1 veya c1"));
        Assert.IsType<SearchAnd>(or.Left);
        Assert.IsType<SearchTerm>(or.Right);
    }

    [Theory]
    [InlineData("hırsızlık DEĞİL yağma")]
    [InlineData("hırsızlık NOT yağma")]
    [InlineData("hırsızlık -yağma")]
    public void Not_Operators(string query)
    {
        var and = Assert.IsType<SearchAnd>(SearchQueryParser.Parse(query));
        Assert.IsType<SearchNot>(and.Right);
    }

    [Fact]
    public void QuotedText_IsPhrase_WithFoldedWords()
    {
        var phrase = Assert.IsType<SearchPhrase>(SearchQueryParser.Parse("“hapis cezası”"));

        Assert.Equal(["hapis", "cezası"], phrase.Normalized);
        Assert.Equal(["hapis", "cezasi"], phrase.Folded);
    }

    [Fact]
    public void AttachedParts_BecomePhrase_AndHyphenInsideWordIsNotExclusion()
    {
        Assert.Equal(["103", "a"], Assert.IsType<SearchPhrase>(SearchQueryParser.Parse("103/a")).Normalized);
        Assert.IsType<SearchPhrase>(SearchQueryParser.Parse("ceza-i"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-taksir")]
    [InlineData("DEĞİL taksir")]
    [InlineData("()")]
    [InlineData("\"\"")]
    [InlineData("??? !!!")]
    public void QueryWithoutPositiveTerm_IsEmpty(string query) =>
        Assert.Null(SearchQueryParser.Parse(query));

    [Fact]
    public void UnbalancedInput_DoesNotThrow()
    {
        Assert.NotNull(SearchQueryParser.Parse("(hırsızlık VEYA yağma"));
        Assert.NotNull(SearchQueryParser.Parse("hırsızlık) VE"));
        Assert.NotNull(SearchQueryParser.Parse("\"açık tırnak"));
    }

    [Theory]
    [InlineData("olduren", "oldur")]
    [InlineData("oldurme", "oldur")]
    [InlineData("oldurmenin", "oldur")]
    [InlineData("cezalandirilir", "cezalandir")]
    [InlineData("tasarlayarak", "tasarlay")]
    [InlineData("yaralama", "yarala")]
    [InlineData("hirsizlik", "hirsizlik")]
    [InlineData("baskan", "baskan")] // gövde 5 harften kısa kalacaktı
    [InlineData("yagma", "yagma")] // kısa sözcük
    [InlineData("5237", "5237")]
    public void TurkishRoot_StripsDerivationalSuffix(string folded, string expected) =>
        Assert.Equal(expected, TurkishRoot.Guess(folded));
}

public class SnippetMarkerTests
{
    private const char S = '\u0002';
    private const char E = '\u0003';

    [Fact]
    public void MarksWordsStartingWithTermOrRoot_AndKeepsExistingMarks()
    {
        var prefixes = SnippetMarker.Prefixes(SearchQueryParser.Parse("kasten öldürme -taksir")!);

        var marked = SnippetMarker.Mark($"Bir insanı {S}kasten{E} öldüren kişi; taksirle Öldürmenin", prefixes, S, E);

        Assert.Equal($"Bir insanı {S}kasten{E} {S}öldüren{E} kişi; taksirle {S}Öldürmenin{E}", marked);
    }

    [Fact]
    public void FoldedQuery_MarksAccentedWords()
    {
        var prefixes = SnippetMarker.Prefixes(SearchQueryParser.Parse("hirsizlik")!);
        Assert.Equal($"{S}Hırsızlık{E} suçu", SnippetMarker.Mark("Hırsızlık suçu", prefixes, S, E));
    }
}
