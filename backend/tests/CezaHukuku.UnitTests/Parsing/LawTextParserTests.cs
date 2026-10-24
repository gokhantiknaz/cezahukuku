using CezaHukuku.Parsing.Legislation;

namespace CezaHukuku.UnitTests.Parsing;

/// <summary>
/// Fixtures/tck-kesit.txt: mevzuat.gov.tr web görünümünden kopyalanan TCK metninden bir kesit (künye, md. 1–5 ve 18,
/// Hayata Karşı Suçlar md. 81–83, md. 342–345 ve Geçici Madde 1, metnin sonundaki ek listeler ve birkaç dipnot).
/// Kesit olduğu için bazı maddeler gerçek bölümlerinin dışında durur; ayrıştırma kuralları için önemli değil.
/// </summary>
public class LawTextParserTests
{
    private static readonly ParsedLaw Tck = LawTextParser.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Parsing", "Fixtures", "tck-kesit.txt")));

    private static IEnumerable<ParsedArticle> AllArticles(ParsedLaw law) =>
        law.Articles.Concat(law.Nodes.SelectMany(Flatten));

    private static IEnumerable<ParsedArticle> Flatten(ParsedNode node) =>
        node.Articles.Concat(node.Children.SelectMany(Flatten));

    private static ParsedArticle Article(string number) => AllArticles(Tck).Single(a => a.Number == number);

    [Fact]
    public void ReadsMetadataFromPreamble()
    {
        Assert.Equal(new LawMetadata("5237", new DateOnly(2004, 9, 26), new DateOnly(2004, 10, 12), "25611"), Tck.Metadata);
    }

    [Fact]
    public void BuildsBookPartChapterTree()
    {
        var books = Tck.Nodes;
        Assert.Equal(["Birinci Kitap", "İkinci Kitap"], books.Select(n => n.Number));
        Assert.Equal(["Genel Hükümler", "Özel Hükümler"], books.Select(n => n.Title));

        var part = Assert.Single(books[1].Children);
        Assert.Equal((ParsedNodeKind.Kisim, "İkinci Kısım", "Kişilere Karşı Suçlar"), (part.Kind, part.Number, part.Title));

        // Yeni bir BÖLÜM önceki bölümü kapatır, aynı kısmın altına girer.
        Assert.Equal(["Birinci Bölüm", "Dokuzuncu Bölüm"], part.Children.Select(c => c.Number));
        Assert.Equal(["81", "82", "83", "342", "343"], part.Children[0].Articles.Select(a => a.Number));
        Assert.Equal(["Geçici 1", "344", "345"], part.Children[1].Articles.Select(a => a.Number));
    }

    [Fact]
    public void TakesArticleTitleFromLineAboveArticle()
    {
        Assert.Equal("Ceza Kanununun amacı", Article("1").Title);
        Assert.Equal("Kasten öldürme", Article("81").Title);
        // Önceki maddenin gövdesinden alınan başlıklar.
        Assert.Equal("Geri verme", Article("18").Title);
        Assert.Equal("Yürürlük", Article("344").Title);
        Assert.Equal("Yürütme", Article("345").Title);
        // Bölüm başlığının hemen altındaki geçici maddenin kendi başlığı yok.
        Assert.Equal("", Article("Geçici 1").Title);
    }

    [Fact]
    public void ParsesClausesAndClosingTextAndDropsFootnoteMarkers()
    {
        var paragraph = Assert.Single(Article("82").Paragraphs);

        Assert.Equal("Kasten öldürme suçunun;", paragraph.Text);
        Assert.Equal(12, paragraph.Items.Count);
        Assert.Equal("d)", paragraph.Items[3].Label);
        Assert.EndsWith("kardeşe karşı,", paragraph.Items[3].Text);
        Assert.Equal((0, (string?)null), (paragraph.Items[^1].Level, paragraph.Items[^1].Label));
        Assert.StartsWith("İşlenmesi halinde", paragraph.Items[^1].Text);
    }

    [Fact]
    public void KeepsClosingLineAfterClausesInsideArticle()
    {
        var paragraph = Article("344").Paragraphs.Single();

        Assert.Equal("yürürlüğe girer.", paragraph.Items[^1].Text);
        Assert.Equal(0, paragraph.Items[^1].Level);
    }

    [Fact]
    public void MarksArticleWithOnlyRepealNoteAsRepealed()
    {
        Assert.True(Article("18").IsRepealed);
        Assert.False(Article("4").IsRepealed); // yalnız bir fıkrası mülga
        Assert.Equal("(Mülga : 29/6/2005 – 5377/1 md.)", Article("4").Paragraphs[1].Text);
    }

    [Fact]
    public void FindsLatestAmendmentNoteInArticle()
    {
        Assert.Null(Article("81").LastAmendedOn);

        // 82'de 29/6/2005 (Ek) ve 12/5/2022 (Değişik) notları var; en yenisi alınır.
        Assert.Equal(new DateOnly(2022, 5, 12), Article("82").LastAmendedOn);
        Assert.Equal("Değişik:12/5/2022-7406/2 md.", Article("82").LastAmendment);

        Assert.Equal(new DateOnly(2005, 6, 29), Article("4").LastAmendedOn); // "(Mülga : 29/6/2005 – 5377/1 md.)"
        Assert.Equal(new DateOnly(2016, 4, 23), Article("18").LastAmendedOn);
    }

    [Theory]
    [InlineData("(Ek cümle: 29/6/2005 – 5377/7 md.)", 2005, 6, 29)]
    [InlineData("(Mülga – 17/4/2013-6460/13 md.)", 2013, 4, 17)]
    [InlineData("(İptal: Anayasa Mahkemesinin 8/10/2015 tarihli ve E.: 2014/140, K.: 2015/85 sayılı Kararı ile.)", 2015, 10, 8)]
    public void AmendmentNoteVariants(string note, int year, int month, int day)
    {
        var article = LawTextParser.Parse($"Başlık\nMadde 1- (1) {note} Metin.").Articles.Single();

        Assert.Equal(new DateOnly(year, month, day), article.LastAmendedOn);
    }

    [Fact]
    public void StopsAtAppendicesAndCountsFootnotes()
    {
        // "5237 SAYILI KANUNA İŞLENEMEYEN HÜKÜMLER" altındaki 5560 sayılı Kanunun geçici maddesi alınmaz.
        Assert.Single(AllArticles(Tck), a => a.Number == "Geçici 1");
        Assert.Equal("345", AllArticles(Tck).Last().Number);
        Assert.Equal(14, AllArticles(Tck).Count());
        Assert.Equal(5, Tck.FootnoteCount);
        Assert.Empty(Tck.Warnings);
    }

    [Fact]
    public void LinksFootnotesToArticlesWhereTheirMarkersAppear()
    {
        var m82 = Tck.Nodes.SelectMany(Flatten).Single(a => a.Number == "82");

        Assert.Equal([33, 34, 35], m82.Footnotes!.Select(f => f.Number));
        Assert.StartsWith("29/6/2005 tarihli ve 5377 sayılı Kanunun 9 uncu maddesiyle", m82.Footnotes![0].Text);
        Assert.Empty(Tck.Nodes.SelectMany(Flatten).Single(a => a.Number == "81").Footnotes!);
    }

    [Fact]
    public void FootnoteOnTitleLine_GoesToTheArticleItTitles_AndMultilineFootnotesAreJoined()
    {
        var law = LawTextParser.Parse("""
            Amaç[1]
            Madde 1- (1) Birinci madde.[2]
            Tanımlar[3]
            Madde 2- (1) İkinci madde.
            [1] Birinci dipnot.
            [2] İkinci dipnot
            iki satırlık.
            [3] Üçüncü dipnot.
            """);

        Assert.Equal([1, 2], law.Articles[0].Footnotes!.Select(f => f.Number));
        Assert.Equal("İkinci dipnot iki satırlık.", law.Articles[0].Footnotes![1].Text);
        Assert.Equal("Tanımlar", law.Articles[1].Title);
        Assert.Equal([3], law.Articles[1].Footnotes!.Select(f => f.Number));
        Assert.Equal(3, law.FootnoteCount);
    }

    [Fact]
    public void ArticleHeaderVariants()
    {
        const string text = """
            BİRİNCİ BÖLÜM
            Genel
            Başlık bir
            MADDE 61. - (1) Bir.
            Başlık iki
            Madde 103/a – (1) İki.
            Ek Madde 2- (1) Üç.
            """;

        var numbers = Flatten(Assert.Single(LawTextParser.Parse(text).Nodes)).Select(a => a.Number);

        Assert.Equal(["61", "103/A", "Ek 2"], numbers);
    }

    [Fact]
    public void ArticlesWithoutHeadingsGoToRoot()
    {
        var law = LawTextParser.Parse("Amaç\nMadde 1- (1) Bir.\nYürürlük\nMadde 2- (1) İki.");

        Assert.Empty(law.Nodes);
        Assert.Equal([("1", "Amaç"), ("2", "Yürürlük")], law.Articles.Select(a => (a.Number, a.Title)));
        Assert.Equal([0, 1], law.Articles.Select(a => a.Order));
    }

    [Fact]
    public void JoinWrappedLines_MergesLowercaseContinuationsFromPdf()
    {
        const string text = "Amaç\nMadde 1- (1) Ceza Kanununun amacı; kişi hak ve özgürlüklerini, kamu düzen ve\ngüvenliğini korumaktır.\n(2) İkinci.";

        var joined = LawTextParser.Parse(text, new LawTextParserOptions { JoinWrappedLines = true }).Articles.Single();
        var notJoined = LawTextParser.Parse(text).Articles.Single();

        Assert.Equal("Ceza Kanununun amacı; kişi hak ve özgürlüklerini, kamu düzen ve güvenliğini korumaktır.", joined.Paragraphs[0].Text);
        Assert.Equal(2, joined.Paragraphs.Count);
        Assert.NotEqual(joined.Paragraphs[0].Text, notJoined.Paragraphs[0].Text);
    }

    [Fact]
    public void WarnsWhenNoArticleFound()
    {
        Assert.Contains(LawTextParser.Parse("Bu bir kanun metni değil.").Warnings, w => w.Contains("madde bulunamadı"));
    }
}
