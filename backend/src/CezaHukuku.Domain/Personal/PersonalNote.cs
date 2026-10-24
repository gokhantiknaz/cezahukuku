using CezaHukuku.Domain.Common;

namespace CezaHukuku.Domain.Personal;

/// <summary>
/// Kullanıcının maddeye yazdığı not; yalnız sahibi görür. <see cref="HighlightId"/> doluysa metindeki bir vurguya
/// bağlıdır (vurgu başına tek not, vurgu silinince not da silinir); boşsa maddenin geneline yazılmıştır.
/// </summary>
public sealed class PersonalNote : Entity
{
    public const int MaxTextLength = 10_000;

    public Guid OwnerId { get; set; }
    public Guid ArticleId { get; set; }
    public Guid? HighlightId { get; set; }

    /// <summary>Düz metin; satır sonları korunur.</summary>
    public string Text { get; set; } = "";
}
