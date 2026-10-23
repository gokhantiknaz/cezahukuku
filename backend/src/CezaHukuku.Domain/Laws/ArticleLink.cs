using CezaHukuku.Domain.Common;

namespace CezaHukuku.Domain.Laws;

public enum ArticleLinkKind
{
    /// <summary>İlgili madde; iki yönlü gösterilir (81 → 82 bağlantısı 82'de de görünür).</summary>
    Ilgili,

    /// <summary>Önceki kanundaki karşılığı (5237 → 765); hedef kütüphanedeyse metni de gösterilir.</summary>
    EskiKanunKarsiligi,
}

/// <summary>
/// Maddeden maddeye bağlantı. Hedef kütüphanede yoksa (ör. 765 sayılı Kanun henüz girilmedi) yalnız etiketle
/// tutulur: "765 s. TCK m. 448"; hedef sonradan girilince bağlantı hedefe çevrilebilir.
/// </summary>
public sealed class ArticleLink : Entity
{
    public Guid SourceArticleId { get; set; }

    /// <summary>Hedef madde; null ise <see cref="ExternalLabel"/> kullanılır.</summary>
    public Guid? TargetArticleId { get; set; }

    public ArticleLinkKind Kind { get; set; }

    /// <summary>Kütüphanede olmayan hedefin adı: "765 s. TCK m. 448".</summary>
    public string? ExternalLabel { get; set; }

    /// <summary>Kısa açıklama: "kısmen karşılar", "nitelikli hâlleri için".</summary>
    public string? Note { get; set; }

    public int SortOrder { get; set; }
}
