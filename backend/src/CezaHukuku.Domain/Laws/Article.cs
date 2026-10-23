using CezaHukuku.Domain.Common;

namespace CezaHukuku.Domain.Laws;

/// <summary>
/// Kanun maddesi. Metni maddenin kendisinde değil, yürürlük aralıklı sürümlerinde tutulur
/// (<see cref="ArticleVersion"/>); böylece "suç tarihindeki metin" gösterilebilir.
/// </summary>
public sealed class Article : Entity
{
    public Guid LawId { get; set; }

    /// <summary>Bağlı olduğu ağaç düğümü; null ise kanunun doğrudan altındadır (bölümlemesiz kısa kanunlar).</summary>
    public Guid? NodeId { get; set; }

    /// <summary>Madde numarası metin olarak: "81", "103/A", "Ek 1", "Geçici 2".</summary>
    public required string Number { get; set; }

    /// <summary>Numaradan türetilen adres parçası: "madde-81", "gecici-madde-2". Kanun içinde tekildir.</summary>
    public required string Slug { get; set; }

    /// <summary>Madde başlığı: "Kasten öldürme".</summary>
    public string Title { get; set; } = "";

    public Visibility Visibility { get; set; }

    /// <summary>Kardeşler (düğüm ve madde) arasındaki sıra.</summary>
    public int SortOrder { get; set; }
}
