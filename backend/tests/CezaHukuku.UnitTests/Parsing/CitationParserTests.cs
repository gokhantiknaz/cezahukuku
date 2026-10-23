using CezaHukuku.Parsing.Legislation;

namespace CezaHukuku.UnitTests.Parsing;

/// <summary>Örnekler TCK'nın gerçek metninden (mevzuat.gov.tr).</summary>
public class CitationParserTests
{
    private static string Linked(string text, Citation c) => text.Substring(c.Start, c.Length);

    [Fact]
    public void SameLaw_SingleArticle()
    {
        const string text = "(1) Bir Türk vatandaşı, 13 üncü maddede yazılı suçlar dışında, Türkiye'de işlediği suçtan";

        var citation = Assert.Single(CitationParser.Find(text));

        Assert.Equal((CitationLawKind.Self, (string?)null, "13"), (citation.LawKind, citation.LawValue, citation.ArticleNumber));
        Assert.Equal("13 üncü maddede", Linked(text, citation));
    }

    [Fact]
    public void SameLaw_ListOfArticles_LinksEachNumber()
    {
        const string text = "(1) 11 ve 12 nci maddelerde belirtilen hallerde, soruşturma";

        var citations = CitationParser.Find(text);

        Assert.Equal(["11", "12"], citations.Select(c => c.ArticleNumber));
        Assert.Equal(["11", "12 nci maddelerde"], citations.Select(c => Linked(text, c)));
    }

    [Fact]
    public void ArticleWithParagraphReference_InParentheses()
    {
        const string text = "Bildirim yükümlülüğünün yerine getirilmemesi (98 inci maddenin birinci fıkrası),";

        var citation = Assert.Single(CitationParser.Find(text));

        Assert.Equal(("98", "98 inci maddenin"), (citation.ArticleNumber, Linked(text, citation)));
    }

    [Fact]
    public void OtherLaw_ByNumber()
    {
        const string text = "31/8/1956 tarihli ve 6831 sayılı Orman Kanununun 108 inci maddesinin birinci fıkrasında yer alan";

        var citation = Assert.Single(CitationParser.Find(text));

        Assert.Equal((CitationLawKind.Number, "6831", "108"), (citation.LawKind, citation.LawValue, citation.ArticleNumber));
        Assert.Equal("108 inci maddesinin", Linked(text, citation));
    }

    [Fact]
    public void OtherLaw_ByNumber_WithLongName()
    {
        const string text = "21/7/1983 tarihli ve 2863 sayılı Kültür ve Tabiat Varlıklarını Koruma Kanununun 74 üncü maddesinin";

        var citation = Assert.Single(CitationParser.Find(text));

        Assert.Equal((CitationLawKind.Number, "2863"), (citation.LawKind, citation.LawValue));
    }

    [Fact]
    public void OtherLaw_ByName()
    {
        const string text = "Suçun konusunun, Ceza Muhakemesi Kanununun 236 ncı maddesinin beşinci ve altıncı fıkraları";

        var citation = Assert.Single(CitationParser.Find(text));

        Assert.Equal((CitationLawKind.Name, "Ceza Muhakemesi Kanunu", "236"),
            (citation.LawKind, citation.LawValue, citation.ArticleNumber));
    }

    [Fact]
    public void OtherLaw_ByName_IgnoresWordsBeforeTheName()
    {
        var citation = Assert.Single(CitationParser.Find("hakim tarafından Ceza Muhakemesi Kanununun 231 inci maddesi uyarınca"));

        Assert.Equal("Ceza Muhakemesi Kanunu", citation.LawValue);
    }

    [Fact]
    public void OwnerCarriesOverWithinSentence_Tck191()
    {
        const string text = "(9) Bu maddede aksine düzenleme bulunmayan hâllerde, Ceza Muhakemesi Kanununun kamu davasının "
            + "açılmasının ertelenmesine ilişkin 171 inci maddesi veya hükmün açıklanmasının geri bırakılmasına ilişkin "
            + "231 inci maddesi hükümleri uygulanır.";

        var citations = CitationParser.Find(text);

        Assert.Equal(["171", "231"], citations.Select(c => c.ArticleNumber));
        Assert.All(citations, c => Assert.Equal((CitationLawKind.Name, "Ceza Muhakemesi Kanunu"), (c.LawKind, c.LawValue)));
    }

    [Fact]
    public void OwnerDoesNotCarryOverToNextSentence()
    {
        var citations = CitationParser.Find("5271 sayılı Kanunun 171 inci maddesi uygulanır. Ancak 188 inci maddede yazılı suçlarda");

        Assert.Equal([(CitationLawKind.Number, "171"), (CitationLawKind.Self, "188")],
            citations.Select(c => (c.LawKind, c.ArticleNumber)));
    }

    [Fact]
    public void ListWithOrdinalAfterEachNumber_Tck192()
    {
        const string text = "sağlık mesleği mensuplarının 279 uncu ve 280 inci maddeler uyarınca";

        var citations = CitationParser.Find(text);

        Assert.Equal(["279", "280"], citations.Select(c => c.ArticleNumber));
        Assert.Equal(["279 uncu", "280 inci maddeler"], citations.Select(c => Linked(text, c)));
    }

    [Fact]
    public void ThisLaw_AfterAnotherWord()
    {
        var citation = Assert.Single(CitationParser.Find("ilgili hükümler ile bu Kanunun 5 inci maddesi"));

        Assert.Equal(CitationLawKind.Self, citation.LawKind);
    }

    [Fact]
    public void NumberWithoutOrdinal_IsNotACitation()
    {
        Assert.Empty(CitationParser.Find("metin 5 madde hâlinde düzenlenmiştir"));
    }

    [Fact]
    public void ThisLaw_Explicitly()
    {
        var citation = Assert.Single(CitationParser.Find("bu Kanunun 53 üncü maddesinde sayılan haklar"));

        Assert.Equal((CitationLawKind.Self, "53"), (citation.LawKind, citation.ArticleNumber));
    }

    [Fact]
    public void TemporaryArticle()
    {
        var citation = Assert.Single(CitationParser.Find("bu Kanunun geçici 1 inci maddesi uyarınca"));

        Assert.Equal("Geçici 1", citation.ArticleNumber);
    }

    [Theory]
    [InlineData("Anayasanın 38 inci maddesi gereğince")]
    [InlineData("Yönetmeliğin 5 inci maddesinde")]
    [InlineData("aynı Kanunun 7 nci maddesi")]
    [InlineData("Kanunun 7 nci maddesi")]
    [InlineData("1234567 sayılı Kanunun 7 nci maddesi")]
    [InlineData("Avrupa İnsan Hakları Sözleşmesinin 6 ncı maddesi")]
    [InlineData("ikinci ve üçüncü fıkralarda")]
    [InlineData("1/6/2005 tarihinde 5 inci fıkra")]
    public void AmbiguousOrNonArticleReferences_AreNotLinked(string text)
    {
        Assert.Empty(CitationParser.Find(text));
    }
}
