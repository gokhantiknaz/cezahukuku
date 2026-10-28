using CezaHukuku.Parsing.Legislation;

namespace CezaHukuku.UnitTests.Parsing;

/// <summary>PDF'ten kopyalanan / çıkarılan metindeki dipnotlar (yapışık üst simge işaret, sayfa altındaki açıklama).</summary>
public class PdfFootnotesTests
{
    // 1632 sayılı Askerî Ceza Kanunu biçiminde: BAP başlığı, başlığa ve cümle sonuna yapışık işaretler, sayfa
    // altındaki dipnotlar (çizgi boşluk dizisi), küçük harfle süren sonraki sayfa, yan yana iki işaret ("910").
    private const string AskeriCeza = """
        ASKERİ CEZA KANUNU
        Kanun Numarası : 1632
        BİRİNCİ KISIM
        Umumi Hükümler
        SEKİZİNCİ BAP41
        Askerî suçlar
        Madde 1 – (Değişik: 22/12/1934-2632/9 md.) Bu kanun askerî şahıslar hakkında
        uygulanır.42 Askerî suç işleyen

        41 Bu başlık, 22/12/1934 tarih ve 2632 sayılı Kanunun 9 uncu maddesi ile gelmiştir.
        42 4/5/2007 tarihli ve 5651 sayılı Kanunun 1 inci maddesiyle bu fıkrada yer alan "şahıs" ibaresi
        "kişi" olarak değiştirilmiştir.
        kişiler hakkında da bu kanun hükümleri tatbik olunur.
        BİRİNCİ FASIL
        Cezalar
        Madde 2 – Cezalar şunlardır:
        1 - Ölüm,
        2 - Ağır hapis.910
        """;

    private static IEnumerable<ParsedArticle> Articles(ParsedLaw law)
    {
        IEnumerable<ParsedArticle> Flatten(ParsedNode node) => node.Articles.Concat(node.Children.SelectMany(Flatten));
        return law.Articles.Concat(law.Nodes.SelectMany(Flatten));
    }

    [Fact]
    public void ConvertsPdfFootnotesToWebFormat()
    {
        // 1–40 numaralı dipnotlar önceki sayfalarda kalmış gibi: numaralar 41'den başlasın diye öncesine eklenir.
        var earlier = string.Concat(Enumerable.Range(1, 40).Select(n => $"Önceki sayfa metni.{n}\n" + new string(' ', 40) + $"\n{n} 1/1/2000 tarihli ve 1 sayılı Kanunla eklenmiştir.\n"));
        var text = PdfFootnotes.Normalize(earlier + AskeriCeza);

        Assert.Contains("SEKİZİNCİ BAP[41]", text);
        Assert.Contains("uygulanır.[42] Askerî suç işleyen", text);
        Assert.Contains("[41] Bu başlık, 22/12/1934 tarih ve 2632 sayılı Kanunun 9 uncu maddesi ile gelmiştir.", text);
        Assert.Contains("[42] 4/5/2007 tarihli ve 5651 sayılı Kanunun 1 inci maddesiyle bu fıkrada yer alan \"şahıs\" ibaresi \"kişi\" olarak değiştirilmiştir.", text);
        // Dipnottan sonra sonraki sayfanın metni madde metnine döner; bentler ("1 - Ölüm") dipnot sanılmaz.
        Assert.Contains("kişiler hakkında da bu kanun hükümleri tatbik olunur.", text.Split("[41] ")[0]);
        Assert.Contains("1 - Ölüm,", text);
        // 9 ve 10'un dipnotu yok: "910" metinde kalır.
        Assert.Contains("Ağır hapis.910", text);
    }

    [Fact]
    public void BapAndFasilAreNodes_AndFootnotesAttachToArticles()
    {
        var earlier = string.Concat(Enumerable.Range(1, 40).Select(n => $"Önceki sayfa metni.{n}\n" + new string(' ', 40) + $"\n{n} 1/1/2000 tarihli ve 1 sayılı Kanunla eklenmiştir.\n"));
        var law = LawTextParser.Parse(earlier + AskeriCeza, new LawTextParserOptions { JoinWrappedLines = true });

        var kisim = Assert.Single(law.Nodes);
        var bap = Assert.Single(kisim.Children);
        Assert.Equal(("Sekizinci Bap", "Askerî suçlar"), (bap.Number, bap.Title));
        var fasil = Assert.Single(bap.Children);
        Assert.Equal(ParsedNodeKind.Ayrim, fasil.Kind);
        Assert.Equal("Birinci Fasıl", fasil.Number);

        var first = Articles(law).Single(a => a.Number == "1");
        Assert.Equal(
            "(Değişik: 22/12/1934-2632/9 md.) Bu kanun askerî şahıslar hakkında uygulanır. Askerî suç işleyen kişiler hakkında da bu kanun hükümleri tatbik olunur.",
            first.Paragraphs.Single().Text);
        Assert.Equal([42], first.Footnotes!.Select(f => f.Number));
        Assert.Empty(law.Warnings);
    }

    [Fact]
    public void AdjacentMarkers_NumberGluedToDate_AndMarkerAfterPunctuationSpace()
    {
        // 1–9 önceki sayfalarda; "112/3/2014" tarih değil: 11 numaralı dipnot + "2/3/2014". Satırın kendisi geçerli bir
        // tarihle başlıyorsa ("22/3/2000 tarih ve …", önceki dipnotun devamı) yapışık numara sayılmaz.
        var earlier = string.Concat(Enumerable.Range(1, 9).Select(n => $"Önceki sayfa.{n}\n{n} 1/1/2000 tarihli ve 1 sayılı Kanunla eklenmiştir.\n"));
        var text = PdfFootnotes.Normalize(earlier + """
            Madde 1- (1) Birinci fıkra.1011
            (2) İkinci fıkra; 12

            10 1/1/2000 tarihli ve 1 sayılı Kanunun 2 nci maddesiyle "eski" ibaresi
            22/3/2000 tarih ve 4551 sayılı Kanunla "yeni" olarak değiştirilmiştir.
            112/3/2014 tarihli ve 6529 sayılı Kanunla değiştirilmiştir.
            12 Bu fıkra 5/5/2005 tarihinde eklenmiştir.
            """);

        Assert.Contains("Birinci fıkra.[10][11]", text);
        Assert.Contains("İkinci fıkra; [12]", text);
        Assert.Contains("[10] 1/1/2000 tarihli ve 1 sayılı Kanunun 2 nci maddesiyle \"eski\" ibaresi 22/3/2000 tarih ve 4551 sayılı Kanunla \"yeni\" olarak değiştirilmiştir.", text);
        Assert.Contains("[11] 2/3/2014 tarihli ve 6529 sayılı Kanunla değiştirilmiştir.", text);
    }

    [Fact]
    public void FootnoteSplitAcrossPages_ContinuesAfterNextSeparator()
    {
        const string separator = "                              ";
        var law = LawTextParser.Parse($"""
            Birinci
            Madde 1- (1) Birinci madde.1
            {separator}
            1 1/1/2000 tarihli ve 1 sayılı Kanunun 2 nci maddesiyle bu fıkrada yer alan "eski"
            İkinci
            Madde 2- (1) İkinci madde metni.2
            {separator}
            ibaresi "yeni" şeklinde değiştirilmiştir.
            2 3/3/2003 tarihli ve 3 sayılı Kanunla eklenmiştir.
            """);

        var articles = Articles(law).ToList();
        Assert.Equal(["1", "2"], articles.Select(a => a.Number));
        Assert.Equal("İkinci madde metni.", articles[1].Paragraphs.Single().Text);
        Assert.Equal("1/1/2000 tarihli ve 1 sayılı Kanunun 2 nci maddesiyle bu fıkrada yer alan \"eski\" ibaresi \"yeni\" şeklinde değiştirilmiştir.",
            articles[0].Footnotes!.Single().Text);
        Assert.Empty(law.Warnings);
    }

    [Fact]
    public void OldLawConventions_From1632()
    {
        // Askerî Ceza Kanunu: başlıklar iki noktayla biter, ilk dipnot harfli bentlerden oluşur, bap başlığının altında
        // ara başlık, ikinci kısımda Romen rakamlı ara başlıklar, sonda büyük-küçük harfli "işlenemeyen hükümler".
        var law = LawTextParser.Parse("""
            ASKERİ CEZA KANUNU1
            BİRİNCİ BAP
            Cezalar
            Umumi hükümler
            Askeri cürüm ve kabahat:
            Madde 1 – Türk Ceza Kanunu esasları tatbik olunur.
            1 - Ölüm cezası askeri cürümdür.

            1 a -Bu Kanunda geçen "erat" tabiri 211 sayılı Kanunla "erbaş ve er" olarak
            değiştirilmiştir.
            b - Adli amirlik 353 sayılı Kanunun geçici 1. maddesi ile sona
            ermiştir.
            İhtiyat askeri şahıslar:
            Madde 2 – İhtiyat askeri şahıslar bu kanuna tabidirler.
            İKİNCİ BAP
            Disiplin cezaları
            I - DİSİPLİN CEZALARININ NEVİLERİ
            Disiplin cezalarının nevileri:
            Madde 3 – (Mülga: 31/1/2013-6413/45 md.)
            22/5/1930 Tarihli Ve 1632 Sayılı Ana Kanuna İşlenemeyen Hükümler
            Madde 38 – Başka kanunun maddesi.
            """, new LawTextParserOptions { JoinWrappedLines = true });

        Assert.Empty(law.Warnings);
        Assert.Equal(["Cezalar — Umumi hükümler", "Disiplin cezaları"], law.Nodes.Select(n => n.Title));
        var roman = Assert.Single(law.Nodes[1].Children);
        Assert.Equal((ParsedNodeKind.Ayrim, "I", "Disiplin Cezalarının Nevileri"), (roman.Kind, roman.Number, roman.Title));

        var articles = Articles(law).ToList();
        Assert.Equal(["1", "2", "3"], articles.Select(a => a.Number));
        Assert.Equal(["Askeri cürüm ve kabahat", "İhtiyat askeri şahıslar", "Disiplin cezalarının nevileri"], articles.Select(a => a.Title));
        // Dipnotun b bendi madde metnine karışmaz.
        Assert.DoesNotContain(articles[0].Paragraphs, p => p.Text.Contains("Adli amirlik"));
        Assert.Equal(1, law.FootnoteCount);
    }

    [Fact]
    public void WebFormatAndTextWithoutFootnotes_AreUnchanged()
    {
        const string web = "Madde 1- Metin.[1]\n[1] Not.";
        Assert.Equal(web, PdfFootnotes.Normalize(web));
        const string plain = "Madde 1- Ceza 5 yıldır.\n1 - Bent";
        Assert.Equal(plain, PdfFootnotes.Normalize(plain));
    }
}
