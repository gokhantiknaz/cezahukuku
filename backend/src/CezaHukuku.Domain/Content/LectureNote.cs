using CezaHukuku.Domain.Common;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Domain.Content;

/// <summary>
/// Ders notu: kanun ya da maddeden bağımsız, kendi sayfası olan konu anlatımı (Ders Notları bölümü). İsteğe bağlı
/// olarak bir kanun grubuna (Temel Ceza Kanunları…), bir kanuna ya da bir maddeye bağlanır; bağlı değilse genel
/// ders notudur. Bağlantı tutarlı tutulur: maddeye bağlı not o maddenin kanununa ve grubuna da bağlıdır.
/// Bağlandığı kanunun görünürlüğü notu daraltmaz.
/// <para>
/// Not bir derse bağlıysa (<see cref="CourseId"/>) o dersin başlık ağacında bir başlıktır: <see cref="ParentId"/> üst
/// başlığı (boşsa birinci düzey), <see cref="SortOrder"/> kardeşler arasındaki sırasıdır. Başlığın numarası, ders
/// yolu ve etkin görünürlüğü (ders → üst başlıklar → kendisi) <see cref="LectureOutline"/> ile hesaplanır.
/// Derse bağlı olmayan not düz ders notudur; görünürlüğü yalnız kendi ayarıdır.
/// </para>
/// </summary>
public sealed class LectureNote : Entity
{
    public const int MaxTitleLength = 300;

    /// <summary>Başlığın dersi; boşsa düz ders notu.</summary>
    public Guid? CourseId { get; set; }

    /// <summary>Üst başlık (aynı dersten); boşsa birinci düzey başlık.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Kardeş başlıklar arasındaki sıra (küçük önce).</summary>
    public int SortOrder { get; set; }

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

/// <summary>
/// Ders notuna eklenen belge (PDF, Word, PowerPoint, Excel, resim). Dosya depoda (<see cref="StorageKey"/>);
/// okunabilen metni (<see cref="Text"/>) aranır. Görünürlüğü ders notununkidir.
/// </summary>
public sealed class LectureNoteFile : Entity
{
    public const long MaxBytes = 50 * 1024 * 1024;
    public const int MaxTitleLength = 300;

    public Guid LectureNoteId { get; set; }
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = "";

    /// <summary>Ders notu sayfasındaki sıra (küçük önce).</summary>
    public int SortOrder { get; set; }

    /// <summary>PDF sayfa sayısı.</summary>
    public int? PageCount { get; set; }

    /// <summary>Belgeden çıkarılan düz metin (arama için); taranmış PDF ve resimde boş.</summary>
    public string Text { get; set; } = "";
}
