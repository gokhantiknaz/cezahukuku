using CezaHukuku.Domain.Common;

namespace CezaHukuku.Domain.Laws;

/// <summary>
/// Maddenin belirli bir yürürlük aralığındaki metni. Bir maddenin sürümlerinin aralıkları çakışmaz;
/// <see cref="ValidTo"/> null olan sürüm hâlen yürürlüktedir.
/// </summary>
public sealed class ArticleVersion : Entity
{
    public Guid ArticleId { get; set; }

    /// <summary>Yürürlük başlangıcı (dahil). Null: başlangıcı bilinmiyor / girilmedi.</summary>
    public DateOnly? ValidFrom { get; set; }

    /// <summary>Yürürlük sonu (dahil). Null: hâlen yürürlükte.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>Bu metni getiren düzenleme: "29/6/2005 tarihli 5377 s.K. m.12".</summary>
    public string? AmendedBy { get; set; }

    public DateOnly? GazetteDate { get; set; }
    public string? GazetteNumber { get; set; }

    /// <summary>Madde bu aralıkta mülga.</summary>
    public bool IsRepealed { get; set; }

    public IReadOnlyList<Paragraph> Paragraphs { get; set; } = [];

    public bool Covers(DateOnly date) =>
        (ValidFrom is null || ValidFrom <= date) && (ValidTo is null || date <= ValidTo);

    public bool Overlaps(ArticleVersion other) =>
        (ValidFrom ?? DateOnly.MinValue) <= (other.ValidTo ?? DateOnly.MaxValue)
        && (other.ValidFrom ?? DateOnly.MinValue) <= (ValidTo ?? DateOnly.MaxValue);

    /// <summary>
    /// Gösterilecek sürümü seçer: tarih verilirse o gün yürürlükte olanı (suç tarihindeki metin), verilmezse
    /// hâlen yürürlükte olanı; o da yoksa en son başlayanı.
    /// </summary>
    public static ArticleVersion? Select(IEnumerable<ArticleVersion> versions, DateOnly? asOf)
    {
        var latestFirst = versions.OrderByDescending(v => v.ValidFrom ?? DateOnly.MinValue).ToList();
        if (asOf is { } date)
            return latestFirst.FirstOrDefault(v => v.Covers(date));

        return latestFirst.FirstOrDefault(v => v.ValidTo is null) ?? latestFirst.FirstOrDefault();
    }
}

/// <summary>
/// Fıkra. Kimliği sürümler arasında korunur (yeni sürüm aynı fıkrayı aynı kimlikle taşır); kişisel vurgu ve
/// notlar bu kimliğe bağlanır.
/// </summary>
/// <param name="Number">Fıkra numarası; tek fıkralı ya da numarasız metinde null.</param>
/// <param name="Text">Fıkranın giriş metni (bentlerden önceki kısım).</param>
public sealed record Paragraph(Guid Id, int? Number, string Text, IReadOnlyList<ParagraphItem> Items);

/// <summary>
/// Fıkra içindeki satır: <paramref name="Level"/> 1 bent ("a)"), 2 alt bent ("1."); 0 ise bentlerden sonra
/// gelen ve fıkrayı bağlayan düz metin ("...hâlinde, ağırlaştırılmış müebbet hapis cezası ile cezalandırılır.").
/// </summary>
public sealed record ParagraphItem(Guid Id, int Level, string? Label, string Text);
