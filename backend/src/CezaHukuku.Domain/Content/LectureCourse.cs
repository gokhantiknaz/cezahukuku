using CezaHukuku.Domain.Common;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Domain.Content;

/// <summary>
/// Ders: Ders Notları bölümünde ders kitabı gibi okunan, numaralı başlık ağacının kökü (ör. "Ceza Hukuku Genel
/// Hükümler"). Başlıkları <see cref="LectureNote"/> kayıtlarıdır (<see cref="LectureNote.CourseId"/>). Görünürlüğü
/// başlıklara miras kalır (<see cref="LectureOutline"/>). Adresi /ders-notlari/{slug}; düz ders notlarıyla aynı adres
/// alanını paylaşır.
/// </summary>
public sealed class LectureCourse : Entity
{
    public const int MaxTitleLength = 300;
    public const int MaxDescriptionLength = 2000;

    public string Title { get; set; } = "";

    /// <summary>Adres: /ders-notlari/{slug}; oluşturulurken addan üretilir, ad değişse de değişmez.</summary>
    public string Slug { get; set; } = "";

    /// <summary>İsteğe bağlı kısa açıklama (düz metin).</summary>
    public string Description { get; set; } = "";

    public Visibility Visibility { get; set; }

    /// <summary>Dersler listesindeki sıra (küçük önce).</summary>
    public int SortOrder { get; set; }
}
