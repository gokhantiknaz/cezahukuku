using CezaHukuku.Domain.Common;

namespace CezaHukuku.Domain.Personal;

/// <summary>Vurgu renkleri; kırmızı bilerek yok (ana renk ve hata rengiyle karışır).</summary>
public enum HighlightColor
{
    Yellow,
    Green,
    Blue,
    Orange,
}

/// <summary>
/// Kullanıcının madde metninde vurguladığı yer. Yalnız sahibi görür. Seçim birden çok fıkraya/bende yayılabilir;
/// her parça ayrı bir <see cref="TextAnchor"/>dır. Madde yeni sürüm aldığında vurgu alıntı metniyle yeni metne
/// yeniden bağlanır (<see cref="TextAnchoring.Locate"/>); bulunamazsa silinmez, "eski sürüme ait" görünür.
/// </summary>
public sealed class Highlight : Entity
{
    public Guid OwnerId { get; set; }
    public Guid ArticleId { get; set; }

    /// <summary>Vurgunun yapıldığı sürüm (ankrajların konumları bu sürümün metnine göredir).</summary>
    public Guid ArticleVersionId { get; set; }

    public HighlightColor Color { get; set; }

    public IReadOnlyList<TextAnchor> Segments { get; set; } = [];
}
