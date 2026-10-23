using CezaHukuku.Domain.Laws;
using CezaHukuku.Parsing.Legislation;
using CezaHukuku.Parsing.Text;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Infrastructure.Persistence;

/// <summary>
/// Sürüm metnindeki atıfları <see cref="ArticleCitation"/> tablosuna yazar. Sürüm eklendiğinde ya da düzeltildiğinde
/// çağrılır (değişiklikler aynı SaveChanges ile kaydedilir); eski kayıtlar için yeniden tarama da buradan yapılır.
/// </summary>
public static class CitationIndex
{
    public static List<ArticleCitation> Extract(ArticleVersion version)
    {
        var rows = new List<ArticleCitation>();
        void Add(Guid paragraphId, Guid? itemId, string text)
        {
            foreach (var c in CitationParser.Find(text))
            {
                var (kind, law) = c.LawKind switch
                {
                    CitationLawKind.Number => (CitationTargetKind.LawNumber, c.LawValue),
                    CitationLawKind.Name => (CitationTargetKind.LawName, TurkishText.Fold(c.LawValue)),
                    _ => (CitationTargetKind.SameLaw, (string?)null),
                };
                rows.Add(new ArticleCitation
                {
                    SourceVersionId = version.Id,
                    SourceArticleId = version.ArticleId,
                    ParagraphId = paragraphId,
                    ItemId = itemId,
                    Start = c.Start,
                    Length = c.Length,
                    TargetKind = kind,
                    TargetLaw = law,
                    TargetArticleSlug = Slugs.ForArticle(c.ArticleNumber),
                });
            }
        }

        foreach (var paragraph in version.Paragraphs)
        {
            Add(paragraph.Id, null, paragraph.Text);
            foreach (var item in paragraph.Items)
                Add(paragraph.Id, item.Id, item.Text);
        }
        return rows;
    }

    /// <summary>Verilen sürümlerin atıflarını yeniler (eski kayıtları siler, yenilerini ekler; SaveChanges çağıranda).</summary>
    public static async Task ReplaceAsync(AppDbContext db, IReadOnlyCollection<ArticleVersion> versions)
    {
        if (versions.Count == 0)
            return;
        var ids = versions.Select(v => v.Id).ToList();
        db.ArticleCitations.RemoveRange(await db.ArticleCitations.Where(c => ids.Contains(c.SourceVersionId)).ToListAsync());
        db.ArticleCitations.AddRange(versions.SelectMany(Extract));
    }

    /// <summary>Kaydedilmek üzere eklenen ya da değiştirilen bütün sürümlerin atıflarını yeniler.</summary>
    public static Task ReplaceForPendingVersionsAsync(AppDbContext db) =>
        ReplaceAsync(db, db.ChangeTracker.Entries<ArticleVersion>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Select(e => e.Entity)
            .ToList());

    /// <summary>Bütün sürümleri yeniden tarar (atıf kuralları değiştiğinde ya da tablo boşken).</summary>
    public static async Task<int> RebuildAllAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.ArticleCitations.ExecuteDeleteAsync(cancellationToken);
        var versionIds = await db.ArticleVersions.Select(v => v.Id).ToListAsync(cancellationToken);
        var total = 0;
        // Parça parça: büyük kütüphanede bütün sürüm metinleri belleğe birden yüklenmez.
        foreach (var chunk in versionIds.Chunk(500))
        {
            var batch = await db.ArticleVersions.AsNoTracking().Where(v => chunk.Contains(v.Id)).ToListAsync(cancellationToken);
            var rows = batch.SelectMany(Extract).ToList();
            db.ArticleCitations.AddRange(rows);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            total += rows.Count;
        }
        return total;
    }
}
