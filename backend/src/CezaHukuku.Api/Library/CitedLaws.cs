using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Legislation;
using CezaHukuku.Parsing.Text;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Library;

/// <summary>
/// Belge metnindeki atıfın kütüphanedeki kanunu: kısaltma kanunun kısa adıyla ("TCK"), numara kanun numarasıyla,
/// ad kanun adıyla eşleşir (aksansız, büyük/küçük harf farksız).
/// </summary>
internal sealed class CitedLaws(IReadOnlyList<Law> laws)
{
    public static async Task<CitedLaws> LoadAsync(AppDbContext db, bool includePrivate, CancellationToken ct) =>
        new(await db.Laws.AsNoTracking().Where(l => includePrivate || l.Visibility == Visibility.Public).ToListAsync(ct));

    public Law? Find(DocumentCitation citation) => citation.Kind switch
    {
        DocumentLawKind.Abbreviation => laws.FirstOrDefault(l =>
            l.ShortName is not null && TurkishText.Fold(l.ShortName) == TurkishText.Fold(citation.LawValue)),
        DocumentLawKind.Number => laws.FirstOrDefault(l => l.Number == citation.LawValue),
        _ => laws.FirstOrDefault(l => TurkishText.Fold(l.Name) == TurkishText.Fold(citation.LawValue)),
    };
}
