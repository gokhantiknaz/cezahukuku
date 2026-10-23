using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Identity;
using CezaHukuku.Parsing.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CezaHukuku.Infrastructure.Persistence;

/// <summary>
/// Geliştirme ortamında boş veritabanına küçük bir kütüphane kesiti ekler (Seed:DemoLibrary). Gerçek metinler
/// içe aktarma hattıyla (M2) girilecek; burada yalnız TCK 81'in metni var, 82–85 başlık olarak duruyor.
/// </summary>
public sealed class DemoLibrarySeeder(AppDbContext db, IOptions<SeedOptions> options, ILogger<DemoLibrarySeeder> logger)
{
    public async Task SeedAsync()
    {
        if (!options.Value.DemoLibrary || await db.LawGroups.IgnoreQueryFilters().AnyAsync())
            return;

        var temel = NewGroup("temel", "Temel Ceza Kanunları", 1);
        var ozel = NewGroup("ozel", "Özel Ceza Kanunları", 2);
        var kabahat = NewGroup("kabahatler", "Kabahatler", 3);
        db.LawGroups.AddRange(temel, ozel, kabahat);

        var tck = new Law
        {
            GroupId = temel.Id,
            Slug = "tck",
            Number = "5237",
            ShortName = "TCK",
            Name = "Türk Ceza Kanunu",
            AcceptedOn = new DateOnly(2004, 9, 26),
            GazetteDate = new DateOnly(2004, 10, 12),
            GazetteNumber = "25611",
            SortOrder = 1,
        };
        db.Laws.AddRange(
            tck,
            NewLaw(temel, "cmk", "5271", "CMK", "Ceza Muhakemesi Kanunu", 2),
            NewLaw(temel, "cgtik", "5275", "CGTİK", "Ceza ve Güvenlik Tedbirlerinin İnfazı Hakkında Kanun", 3),
            NewLaw(ozel, "5607", "5607", null, "Kaçakçılıkla Mücadele Kanunu", 1),
            NewLaw(ozel, "tmk", "3713", "TMK", "Terörle Mücadele Kanunu", 2),
            NewLaw(ozel, "6136", "6136", null, "Ateşli Silahlar ve Bıçaklar ile Diğer Aletler Hakkında Kanun", 3),
            NewLaw(kabahat, "5326", "5326", null, "Kabahatler Kanunu", 1));

        var genel = NewNode(tck, null, LawNodeKind.Kitap, "Birinci Kitap", "Genel Hükümler", 1);
        var ozelHukumler = NewNode(tck, null, LawNodeKind.Kitap, "İkinci Kitap", "Özel Hükümler", 2);
        var kisilere = NewNode(tck, ozelHukumler, LawNodeKind.Kisim, "İkinci Kısım", "Kişilere Karşı Suçlar", 2);
        var hayata = NewNode(tck, kisilere, LawNodeKind.Bolum, "Birinci Bölüm", "Hayata Karşı Suçlar", 1);
        db.LawNodes.AddRange(genel, ozelHukumler, kisilere, hayata);

        string[][] articles =
        [
            ["81", "Kasten öldürme"],
            ["82", "Nitelikli haller"],
            ["83", "Kasten öldürmenin ihmali davranışla işlenmesi"],
            ["84", "İntihara yönlendirme"],
            ["85", "Taksirle öldürme"],
        ];
        foreach (var (item, index) in articles.Select((a, i) => (a, i)))
        {
            var article = new Article
            {
                LawId = tck.Id,
                NodeId = hayata.Id,
                Number = item[0],
                Slug = Slugs.ForArticle(item[0]),
                Title = item[1],
                SortOrder = index + 1,
            };
            db.Articles.Add(article);

            if (item[0] == "81")
            {
                db.ArticleVersions.Add(new ArticleVersion
                {
                    ArticleId = article.Id,
                    ValidFrom = new DateOnly(2005, 6, 1),
                    Paragraphs =
                    [
                        new Paragraph(Guid.CreateVersion7(), 1,
                            "Bir insanı kasten öldüren kişi, müebbet hapis cezası ile cezalandırılır.", []),
                    ],
                });
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Örnek kütüphane verisi eklendi.");
    }

    private static LawGroup NewGroup(string slug, string title, int order) =>
        new() { Slug = slug, Title = title, SortOrder = order };

    private static Law NewLaw(LawGroup group, string slug, string number, string? shortName, string name, int order) =>
        new() { GroupId = group.Id, Slug = slug, Number = number, ShortName = shortName, Name = name, SortOrder = order };

    private static LawNode NewNode(Law law, LawNode? parent, LawNodeKind kind, string number, string title, int order) =>
        new() { LawId = law.Id, ParentId = parent?.Id, Kind = kind, Number = number, Title = title, SortOrder = order };
}
