using CezaHukuku.Parsing.Decisions;

namespace CezaHukuku.UnitTests.Parsing;

public class DecisionCitationFinderTests
{
    [Theory]
    [InlineData("Yargıtay 1. CD, 12.03.2019, 2018/1234 E., 2019/567 K. sayılı kararı", "1. Ceza Dairesi", "2018/1234", "2019/567", "2019-03-12")]
    [InlineData("Yargıtay 4. Ceza Dairesinin 12/03/2020 tarihli ve 2019/1 Esas, 2020/2 Karar sayılı kararı", "4. Ceza Dairesi", "2019/1", "2020/2", "2020-03-12")]
    [InlineData("(Y. 4. CD, E. 2019/1, K. 2020/2, T. 12.03.2020)", "4. Ceza Dairesi", "2019/1", "2020/2", "2020-03-12")]
    [InlineData("Yargıtay Ceza Genel Kurulunun 15 Ekim 2019 tarihli, 2017/1-123 E. ve 2019/456 K. sayılı kararı", "Ceza Genel Kurulu", "2017/1-123", "2019/456", "2019-10-15")]
    [InlineData("YCGK'nın 2017/1-123 E., 2019/456 K. sayılı ve 15.10.2019 tarihli kararı", "Ceza Genel Kurulu", "2017/1-123", "2019/456", "2019-10-15")]
    [InlineData("Esas No: 2018/1234 Karar No: 2019/567 Karar Tarihi: 12.03.2019 (Yargıtay 1. CD)", null, "2018/1234", "2019/567", "2019-03-12")]
    public void Yargitay(string text, string? chamber, string esas, string karar, string date)
    {
        var citation = Assert.Single(DecisionCitationFinder.Find(text));

        // Mahkeme künyenin arkasında yazılmışsa ("… (Yargıtay 1. CD)") alınmaz.
        Assert.Equal(chamber is null ? null : CitedCourt.Yargitay, citation.Court);
        Assert.Equal(chamber, citation.Chamber);
        Assert.Equal(esas, citation.CaseNumber);
        Assert.Equal(karar, citation.DecisionNumber);
        Assert.Equal(DateOnly.Parse(date), citation.Date);
    }

    [Fact]
    public void Span_CoversCourtToLastPart()
    {
        const string text = "Nitekim Yargıtay 1. CD, 12.03.2019, 2018/1234 E., 2019/567 K. sayılı kararında da belirtildiği üzere";

        var citation = Assert.Single(DecisionCitationFinder.Find(text));

        Assert.Equal("Yargıtay 1. CD, 12.03.2019, 2018/1234 E., 2019/567 K.", text.Substring(citation.Start, citation.Length));
    }

    [Fact]
    public void ConstitutionalCourt_IndividualApplication()
    {
        var citation = Assert.Single(DecisionCitationFinder.Find("AYM, Ahmet Yılmaz, B. No: 2014/12345, 12/2/2016, § 45"));

        Assert.Equal(CitedCourt.AnayasaMahkemesi, citation.Court);
        Assert.True(citation.IsApplication);
        Assert.Equal(("2014/12345", (string?)null), (citation.CaseNumber, citation.DecisionNumber));
        Assert.Equal(new DateOnly(2016, 2, 12), citation.Date);
    }

    [Fact]
    public void ConstitutionalCourt_NormReview()
    {
        var citation = Assert.Single(DecisionCitationFinder.Find("Anayasa Mahkemesinin 8.12.2016 tarihli ve E.2016/33, K.2016/196 sayılı kararıyla"));

        Assert.Equal((CitedCourt.AnayasaMahkemesi, "2016/33", "2016/196"), (citation.Court!.Value, citation.CaseNumber!, citation.DecisionNumber!));
        Assert.Equal(new DateOnly(2016, 12, 8), citation.Date);
    }

    [Fact]
    public void List_SeparatedBySemicolons_KeepsEachCourtAndDate()
    {
        var citations = DecisionCitationFinder.Find(
            "(Y. 1. CD, 12.03.2019, 2018/1 E., 2019/2 K.; 2. CD, 2018/3 E., 2019/4 K., 13.03.2019; YCGK 2017/1-5 E., 2018/6 K.)");

        Assert.Equal(["1. Ceza Dairesi", "2. Ceza Dairesi", "Ceza Genel Kurulu"], citations.Select(c => c.Chamber));
        Assert.All(citations, c => Assert.Equal(CitedCourt.Yargitay, c.Court));
        Assert.Equal([new DateOnly(2019, 3, 12), new DateOnly(2019, 3, 13), null], citations.Select(c => c.Date));
    }

    [Fact]
    public void DateOfNextSentence_IsNotTaken()
    {
        var citation = Assert.Single(DecisionCitationFinder.Find("Yargıtay 1. CD 2018/1 E., 2019/2 K. Sanık 12.03.2020 tarihinde yakalanmıştır."));

        Assert.Null(citation.DateText);
    }

    [Fact]
    public void InvalidDate_IsKeptAsText()
    {
        var citation = Assert.Single(DecisionCitationFinder.Find("Yargıtay 1. CD, 31.02.2019, 2018/1 E., 2019/2 K."));

        Assert.Equal("31.02.2019", citation.DateText);
        Assert.Null(citation.Date);
    }

    [Theory]
    [InlineData("2019/123 Esas sayılı dosyamızda")] // mahkemesiz, tarihsiz esas: yerel dosya
    [InlineData("TCK 81 ve 2019/123 sayılı genelge")] // işaretsiz numara
    [InlineData("12.03.2019 tarihinde Yargıtay'a başvurdu")]
    public void NotCitations(string text) =>
        Assert.Empty(DecisionCitationFinder.Find(text));

    [Theory]
    [InlineData("1. CD", "1. Ceza Dairesi")]
    [InlineData("1. Ceza Dairesi", "1. Ceza Dairesi")]
    [InlineData("CGK", "Ceza Genel Kurulu")]
    [InlineData("Ceza Genel Kurulu", "Ceza Genel Kurulu")]
    [InlineData("Büyük Daire", "Büyük Daire")]
    public void NormalizeChamber(string stored, string expected) =>
        Assert.Equal(expected, DecisionCitationFinder.NormalizeChamber(stored));
}

public class DecisionCitationRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 25);

    private static IReadOnlyList<string> Codes(string text) =>
        [.. DecisionCitationRules.Check(Assert.Single(DecisionCitationFinder.Find(text)), Today).Select(f => f.Code)];

    [Fact]
    public void ConsistentCitation_HasNoFindings() =>
        Assert.Empty(Codes("Yargıtay 1. CD, 12.03.2019, 2018/1234 E., 2019/567 K."));

    [Fact]
    public void DecisionYear_MustMatchDateYear() =>
        Assert.Equal(["karar_yili"], Codes("Yargıtay 1. CD, 12.03.2020, 2018/1234 E., 2019/567 K."));

    [Fact]
    public void CaseYear_CannotBeAfterDecisionYear() =>
        Assert.Equal(["esas_yili"], Codes("Yargıtay 1. CD, 12.03.2019, 2020/1234 E., 2019/567 K."));

    [Fact]
    public void FutureAndInvalidDates()
    {
        Assert.Contains("ileri_tarih", Codes("Yargıtay 1. CD, 12.03.2027, 2026/1 E., 2027/2 K."));
        Assert.Contains("gecersiz_tarih", Codes("Yargıtay 1. CD, 31.02.2019, 2018/1 E., 2019/2 K."));
    }

    [Fact]
    public void MissingParts_AreListed()
    {
        var finding = Assert.Single(DecisionCitationRules.Check(Assert.Single(DecisionCitationFinder.Find("Yargıtay 2019/567 K. sayılı kararı")), Today));

        Assert.Equal(("eksik", CheckSeverity.Warning), (finding.Code, finding.Severity));
        Assert.Equal("Künyede eksik: daire, esas no, tarih.", finding.Message);
    }

    [Fact]
    public void ImpossibleChamber() =>
        Assert.Equal(["daire_no"], Codes("Yargıtay 31. CD, 12.03.2019, 2018/1 E., 2019/2 K."));

    [Fact]
    public void IndividualApplication_NeedsNoDecisionNumber()
    {
        Assert.Empty(Codes("AYM, B. No: 2014/12345, 12/2/2016"));
        Assert.Equal(["esas_yili"], Codes("AYM, B. No: 2017/12345, 12/2/2016"));
    }
}
