using CezaHukuku.Domain.Common;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Domain.Content;

/// <summary>
/// Ders notu: kanun ya da maddeden bağımsız, kendi sayfası olan konu anlatımı (Ders Notları bölümü). İsteğe bağlı
/// olarak bir kanun grubuna (Temel Ceza Kanunları…), bir kanuna ya da bir maddeye bağlanır; bağlı değilse genel
/// ders notudur. Bağlantı tutarlı tutulur: maddeye bağlı not o maddenin kanununa ve grubuna da bağlıdır.
/// Görünürlüğü yalnız kendi ayarıdır (bağlandığı kanunun görünürlüğü notu daraltmaz).
/// </summary>
public sealed class LectureNote : Entity
{
    public const int MaxTitleLength = 300;

    public string Title { get; set; } = "";

    /// <summary>Adres: /ders-notlari/{slug}; başlıktan üretilir, kayıtlar arasında tekildir.</summary>
    public string Slug { get; set; } = "";

    /// <summary>İzin verilen etiketlerle sınırlanmış HTML.</summary>
    public string Html { get; set; } = "";

    /// <summary>HTML'in düz metni (arama ve özet için).</summary>
    public string PlainText { get; set; } = "";

    public Visibility Visibility { get; set; }

    public Guid? LawGroupId { get; set; }
    public Guid? LawId { get; set; }
    public Guid? ArticleId { get; set; }
}
