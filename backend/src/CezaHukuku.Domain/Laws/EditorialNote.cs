using CezaHukuku.Domain.Common;

namespace CezaHukuku.Domain.Laws;

public enum EditorialNoteKind
{
    /// <summary>Maddenin gerekçesi (ilk metnin ya da bir değişikliğin).</summary>
    Gerekce,

    /// <summary>Editörün maddeye açıklaması / şerhi.</summary>
    Aciklama,

    /// <summary>Ders notu (madde ya da kanun düzeyinde).</summary>
    DersNotu,

    /// <summary>Kanunun genel gerekçesi (kanun düzeyinde).</summary>
    GenelGerekce,
}

/// <summary>
/// Yönetimin girdiği, herkesin (görünürlüğüne göre) okuduğu not: gerekçe, açıklama, ders notu. Maddeye ya da kanunun
/// tamamına bağlıdır. Kişisel vurgu ve notlar ayrı tutulur (M4). Metin temizlenmiş HTML olarak saklanır; arama için
/// düz metni de tutulur.
/// </summary>
public sealed class EditorialNote : Entity
{
    public Guid LawId { get; set; }

    /// <summary>Bağlı olduğu madde; null ise kanun düzeyinde not.</summary>
    public Guid? ArticleId { get; set; }

    public EditorialNoteKind Kind { get; set; }

    /// <summary>İsteğe bağlı başlık: "5377 sayılı Kanunla yapılan değişikliğin gerekçesi".</summary>
    public string? Title { get; set; }

    /// <summary>İzin verilen etiketlerle sınırlanmış HTML.</summary>
    public string Html { get; set; } = "";

    /// <summary>HTML'in düz metni (arama ve özet için).</summary>
    public string PlainText { get; set; } = "";

    /// <summary>Kendi ayarı; geçerli görünürlük bağlı olduğu madde/kanunla daralır.</summary>
    public Visibility Visibility { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Not maddeye mi ait, kanun düzeyinde mi olmalı; tür bunu belirler.</summary>
    public static bool IsArticleLevel(EditorialNoteKind kind) => kind is not EditorialNoteKind.GenelGerekce;

    public static bool IsLawLevel(EditorialNoteKind kind) => kind is EditorialNoteKind.GenelGerekce or EditorialNoteKind.DersNotu;
}
