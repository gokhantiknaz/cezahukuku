using CezaHukuku.Parsing.Text;

namespace CezaHukuku.UnitTests.Parsing;

/// <summary>Spec 0004: başlık listesi okuma kuralları (R-4), hatalar ve uyarılar (R-5). Örnekler "Ceza Hukuku Ders
/// Notları — Başlıklar" PDF'inin yapısından (PDF'ten çıkarılan metin: her başlık bir satır, sayfa numarası ayrı satır).</summary>
public class LectureHeadingParserTests
{
    private const string Chapter2 = """
        2
        2. BÖLÜM – SUÇ TEORİSİ
        2.1. Suçun Unsurları
        2.1.1. Tipiklik
        2.1.2. Hukuka Aykırılık
        2.3. Tipikliğin Maddi Unsurları
        2.3.2. Fiil
        2.3.2.5. Hareketin Şekline Göre Suçlar
        2.3.2.5.2. İhmali Suçlar
        2.3.2.5.2.1. Gerçek İhmali Suçlar
        3
        2.3.2.5.2.2. Gerçek Olmayan İhmali Suçlar
        2.9. İçtima
        2.9.1. Genel Olarak
        2.9.2. Fiilin Tekliği ve Çokluğu
        2.9.3. Genel Olarak
        """;

    private static HeadingImport Parse(string text, string? title = null, int? chapter = null, bool suggest = true) =>
        LectureHeadingParser.Parse(text, title, chapter, suggest);

    [Fact]
    public void SingleChapter_SuggestsTitleAndNumber_NumbersFollowTheChapter()
    {
        var result = Parse(Chapter2);

        Assert.Equal(("Suç Teorisi", 2), (result.SuggestedTitle, result.SuggestedChapterNumber));
        Assert.Equal(("Suç Teorisi", 2), (result.Title, result.ChapterNumber));
        Assert.Empty(result.Errors);
        // Sayfa numaraları (2, 3) yok sayılır; bölüm satırı dersin kendisidir.
        Assert.Equal(13, result.Headings.Count);
        Assert.Equal(["2.1", "2.1.1", "2.1.2", "2.2", "2.2.1", "2.2.1.1", "2.2.1.1.1", "2.2.1.1.1.1"],
            result.Headings.Take(8).Select(h => h.Number));
        Assert.Equal([1, 2, 2, 1, 2, 3, 4, 5, 5], result.Headings.Take(9).Select(h => h.Depth));
        Assert.Equal(("Gerçek Olmayan İhmali Suçlar", "2.3.2.5.2.2"), (result.Headings[8].Title, result.Headings[8].SourceNumber));
    }

    [Fact]
    public void MatchingNumbers_GiveNoWarning_GapsAndDuplicateSiblingsWarn()
    {
        var result = Parse(Chapter2);

        // PDF'te bu alıntıda 2.2 ve 2.3.1 yok: ağaç satır sırasıyla kurulur, numara farkı uyarıdır.
        Assert.Contains(result.Warnings, w => w.Line == 6 && w.Message.Contains("2.3") && w.Message.Contains("2.2"));
        Assert.DoesNotContain(result.Warnings, w => w.Line == 4); // 2.1.1 aynı
        // 2.9.1 ve 2.9.3 "Genel Olarak": aynı üst başlık altında aynı ad.
        Assert.Contains(result.Warnings, w => w.Line == 16 && w.Message.Contains("Aynı üst başlık altında aynı ad"));
    }

    [Fact]
    public void WholeBook_IsOneUnnumberedCourse_ChaptersAreFirstLevel()
    {
        var result = Parse("""
            CEZA HUKUKU DERS NOTLARI
            BAŞLIKLAR
            1. BÖLÜM – CEZA HUKUKUNA GİRİŞ
            1.1. Suç ve Ceza Kavramları
            1.8.3.6. Asker Kişiler (NATO Askerleri)
            2. BÖLÜM – SUÇ TEORİSİ
            2.1. Suçun Unsurları
            3. BÖLÜM – YAPTIRIM
            3.1. CEZA HUKUKU YAPTIRIMLARI
            """, chapter: 5);

        Assert.Equal(("Ceza Hukuku Ders Notları", null), (result.SuggestedTitle, result.SuggestedChapterNumber));
        Assert.Null(result.ChapterNumber); // öneriler uygulanınca elle girilen 5 de kalkar
        Assert.Equal(["1", "1.1", "1.1.1", "2", "2.1", "3", "3.1"], result.Headings.Select(h => h.Number));
        Assert.Equal(["Ceza Hukukuna Giriş", "Suç ve Ceza Kavramları", "Asker Kişiler (NATO Askerleri)", "Suç Teorisi",
            "Suçun Unsurları", "Yaptırım", "Ceza Hukuku Yaptırımları"], result.Headings.Select(h => h.Title));
        // 1.8.3.6 düzey atlıyor (1.1'den sonra üçüncü düzey yok): hata.
        Assert.Contains(result.Errors, e => e.Line == 5 && e.Message.Contains("düzey atlanmış"));
    }

    [Theory]
    [InlineData("CEZA HUKUKUNA GİRİŞ", "Ceza Hukukuna Giriş")]
    [InlineData("İDARENİN DÜZENLEYİCİ İŞLEMLERLE SUÇ VE CEZA YARATMASI YASAĞI", "İdarenin Düzenleyici İşlemlerle Suç ve Ceza Yaratması Yasağı")]
    [InlineData("VE İLE BAŞLAYAN", "Ve ile Başlayan")]
    [InlineData("ALKOL YA DA UYUŞTURUCU", "Alkol ya da Uyuşturucu")]
    [InlineData("TCK (ÖZEL)", "Tck (Özel)")]
    [InlineData("Yasama Sorumsuzluğu", "Yasama Sorumsuzluğu")]
    [InlineData("Asker Kişiler (NATO Askerleri)", "Asker Kişiler (NATO Askerleri)")]
    [InlineData("1. VE 2. FIKRA", "1. ve 2. Fıkra")]
    public void FixCase_OnlyAllCapitalTitles(string input, string expected) =>
        Assert.Equal(expected, LectureHeadingParser.FixCase(input));

    [Fact]
    public void UnnumberedLine_ContinuesThePreviousHeading_HeaderLinesBeforeFirstHeading()
    {
        var result = Parse("""
            Ceza Hukuku Ders Notları

            1. BÖLÜM – CEZA
            HUKUKUNA GİRİŞ
            1.1. Ceza Hukukunun Hukuk Sistemi
            İçindeki Yeri
            """);

        Assert.Equal("Ceza Hukukuna Giriş", result.SuggestedTitle);
        Assert.Equal("Ceza Hukukunun Hukuk Sistemi İçindeki Yeri", Assert.Single(result.Headings).Title);
    }

    [Fact]
    public void NoChapterLine_HeaderSuggestsTitle_ChapterFromForm()
    {
        const string text = """
            Suç Teorisi
            2.1. Suçun Unsurları
            2.1.1. Tipiklik
            """;
        var withoutNumber = Parse(text);
        Assert.Equal(("Suç Teorisi", null), (withoutNumber.Title, withoutNumber.ChapterNumber));
        Assert.Contains(withoutNumber.Errors, e => e.Line == 2 && e.Message.Contains("bölüm numarası verin"));

        var withNumber = Parse(text, "Suç Teorisi", 2, suggest: false);
        Assert.Empty(withNumber.Errors);
        Assert.Equal(["2.1", "2.1.1"], withNumber.Headings.Select(h => h.Number));
    }

    [Fact]
    public void Suggestions_ApplyOnlyWhenTextChanged()
    {
        var kept = Parse(Chapter2, "Benim adım", 7, suggest: false);
        Assert.Equal(("Benim adım", 7), (kept.Title, kept.ChapterNumber));
        Assert.Equal(("Suç Teorisi", 2), (kept.SuggestedTitle, kept.SuggestedChapterNumber));
        // Bölüm numarası 7 iken 2 ile başlayan satırlar ve 2. bölüm satırı hatadır.
        Assert.Contains(kept.Errors, e => e.Line == 3 && e.Message.Contains("7 ile başlamıyor"));
        Assert.Contains(kept.Errors, e => e.Line == 2 && e.Message.Contains("bölüm satırı"));
    }

    [Fact]
    public void Errors_SkippedLevel_EmptyTitle_NoHeadings_LongTitle_TooLongText()
    {
        Assert.Contains(Parse("1. Bir\n1.1.1. Üç", "Ders").Errors, e => e.Line == 2 && e.Message.Contains("düzey atlanmış"));
        Assert.Contains(Parse("1. Bir", "  ", suggest: false).Errors, e => e.Line == 0 && e.Message.Contains("Ders adı boş"));
        Assert.Contains(Parse("Yalnız düz metin\nbaşka satır", "Ders").Errors, e => e.Message.Contains("başlık bulunamadı"));
        Assert.Contains(Parse("1. " + new string('a', 301), "Ders").Errors, e => e.Line == 1 && e.Message.Contains("300 karakter"));
        Assert.Empty(Parse("1. " + new string('a', 300), "Ders").Errors);
        Assert.Contains(Parse("1. Bir", new string('d', 301)).Errors, e => e.Line == 0 && e.Message.Contains("Ders adı"));

        var tooLong = Parse(new string('x', LectureHeadingParser.MaxTextLength + 1), "Ders");
        Assert.Equal("Metin 200.000 karakterden uzun olamaz.", Assert.Single(tooLong.Errors).Message);
        Assert.Empty(tooLong.Headings);
        Assert.DoesNotContain(Parse(new string(' ', LectureHeadingParser.MaxTextLength), "Ders").Errors,
            e => e.Message.Contains("200.000"));
    }

    [Fact]
    public void AtMost300Headings()
    {
        static string Headings(int count) => string.Join('\n', Enumerable.Range(1, count).Select(i => $"{i}. Başlık {i}"));

        var max = Parse(Headings(300), "Ders");
        Assert.Empty(max.Errors);
        Assert.Equal("300", max.Headings[^1].Number);
        Assert.Contains(Parse(Headings(301), "Ders").Errors, e => e.Line == 0 && e.Message.Contains("En çok 300 başlık"));
    }
}
