using CezaHukuku.Domain.Laws;
using NpgsqlTypes;

namespace CezaHukuku.Infrastructure.Search;

/// <summary>Arama sonucunun türü (filtre ve gruplama).</summary>
public enum SearchKind
{
    /// <summary>Madde metni (yürürlükteki sürüm).</summary>
    Article,
    Gerekce,
    Aciklama,
    DersNotu,
    GenelGerekce,
    Ictihat,
    NormDenetimi,

    /// <summary>Kullanıcının kendi notu (yalnız sahibinin aramasında).</summary>
    Note,

    /// <summary>Kullanıcının kendi vurgusu (yalnız sahibinin aramasında).</summary>
    Highlight,

    /// <summary>Kullanıcının yüklediği PDF'in bir sayfası (yalnız sahibinin aramasında).</summary>
    Document,

    /// <summary>Kullanıcının sesli notunun yazıya dökülmüş bir bölümü (yalnız sahibinin aramasında).</summary>
    VoiceNote,
}

/// <summary>
/// Arama indeksinin bir satırı: aranabilir her içerik (madde, not, karar, kişisel not/vurgu) tek tabloda.
/// Kaynak değiştikçe <see cref="SearchIndexer"/> yeniden üretir. Görünürlük satırda hesaplanmış olarak durur (kanun →
/// düğüm → madde → not daralması); kişisel satırlarda <see cref="OwnerId"/> dolu ve yalnız sahibi bulur.
/// Metinler uygulamada Türkçe kurallarla küçültülür (<c>*_norm</c>) ve aksansızlaştırılır (<c>*_fold</c>);
/// <see cref="Search"/> bunlardan veritabanında üretilen tsvector'dür (başlık A, gövde B ağırlıklı).
/// </summary>
public sealed class SearchDoc
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public SearchKind Kind { get; set; }

    /// <summary>Kaynak kaydın kimliği (madde, not, karar, vurgu).</summary>
    public Guid SourceId { get; set; }

    /// <summary>Kaynağın üst kaydı: PDF sayfası ve ses bölümünde ekin kimliği (ek silinince satırları birlikte silinir).</summary>
    public Guid? ParentId { get; set; }

    public Guid? OwnerId { get; set; }
    public Visibility Visibility { get; set; }

    /// <summary>İlgili kanunlar (kararda bağlı maddelerin kanunları) — kanun filtresi.</summary>
    public Guid[] LawIds { get; set; } = [];

    /// <summary>İlgili maddelerin üst düğümleri (kitap, kısım, bölüm) — bölüm filtresi.</summary>
    public Guid[] NodeIds { get; set; } = [];

    /// <summary>Gösterilen başlık: "Madde 81 — Kasten öldürme", "Gerekçe — Madde 81".</summary>
    public required string Title { get; set; }

    /// <summary>Bağlam: kanun kısaltması ya da mahkeme ("TCK", "Yargıtay 1. Ceza Dairesi").</summary>
    public string? Context { get; set; }

    public string Body { get; set; } = "";

    /// <summary>Sonuca tıklanınca gidilecek adres: "/tck/madde-81?sekme=gerekce".</summary>
    public required string Url { get; set; }

    /// <summary>Yürürlük ya da karar tarihi.</summary>
    public DateOnly? Date { get; set; }

    public string TitleNorm { get; set; } = "";
    public string BodyNorm { get; set; } = "";
    public string TitleFold { get; set; } = "";
    public string BodyFold { get; set; } = "";

    public NpgsqlTsVector? Search { get; set; }
}
