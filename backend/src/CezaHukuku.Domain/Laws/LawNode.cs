using CezaHukuku.Domain.Common;

namespace CezaHukuku.Domain.Laws;

public enum LawNodeKind
{
    Kitap,
    Kisim,
    Bolum,
    Ayrim,
}

/// <summary>
/// Kanunun bölümleme ağacındaki bir düğüm (Kitap › Kısım › Bölüm › Ayrım). Kanunlar bu seviyeleri farklı
/// kullandığından sıra serbesttir; yalnız düğümün aynı kanunda olması ve döngü oluşmaması zorunludur.
/// </summary>
public sealed class LawNode : Entity
{
    public Guid LawId { get; set; }

    /// <summary>Üst düğüm; null ise kanunun doğrudan altındadır.</summary>
    public Guid? ParentId { get; set; }

    public LawNodeKind Kind { get; set; }

    /// <summary>Düğümün sıra adı: "İkinci Kitap", "Birinci Bölüm".</summary>
    public required string Number { get; set; }

    public required string Title { get; set; }

    /// <summary>Kendi ayarı; geçerli görünürlük üst öğelerle birlikte hesaplanır (bkz. <see cref="LawOutline"/>).</summary>
    public Visibility Visibility { get; set; }

    /// <summary>Kardeşler (düğüm ve madde) arasındaki sıra.</summary>
    public int SortOrder { get; set; }
}
