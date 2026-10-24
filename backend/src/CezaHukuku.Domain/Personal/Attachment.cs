using CezaHukuku.Domain.Common;

namespace CezaHukuku.Domain.Personal;

public enum AttachmentKind
{
    /// <summary>Kullanıcının yüklediği PDF belge (karar, makale, ders notu…).</summary>
    Pdf,

    /// <summary>Sesli not (tarayıcıda kaydedilir).</summary>
    Audio,
}

public enum AttachmentStatus
{
    /// <summary>Yüklendi, arka planda metni çıkarılacak / yazıya dökülecek.</summary>
    Pending,

    Ready,

    /// <summary>İşlenemedi (bozuk dosya vb.); <see cref="Attachment.Error"/> nedenini söyler. Dosya yine açılabilir.</summary>
    Failed,
}

/// <summary>
/// Kullanıcının dosyası: PDF ya da sesli not. Yalnız sahibi görür. Dosya depoda (<see cref="StorageKey"/>), çıkarılan
/// metin parçalar hâlinde (<see cref="AttachmentSegment"/>: PDF'te sayfa, seste zaman damgalı bölüm) tutulur ve
/// aranır. Bir maddeye bağlanabilir (sesli not madde okunurken alınır).
/// </summary>
public sealed class Attachment : Entity
{
    public const long MaxPdfBytes = 50 * 1024 * 1024;
    public const long MaxAudioBytes = 25 * 1024 * 1024;
    public const int MaxTitleLength = 300;

    public Guid OwnerId { get; set; }
    public Guid? ArticleId { get; set; }
    public AttachmentKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = "";

    public AttachmentStatus Status { get; set; }
    public string? Error { get; set; }

    /// <summary>PDF sayfa sayısı.</summary>
    public int? PageCount { get; set; }

    /// <summary>PDF'te metin katmanı olmayan (taranmış) sayfa sayısı; bunlar OCR'sız aranamaz.</summary>
    public int? ScannedPageCount { get; set; }

    /// <summary>Ses süresi (yazıya dökme servisi bildirirse).</summary>
    public double? DurationSeconds { get; set; }

    /// <summary>Sesli not yazıya döküldü mü (servis kapalıysa ya da kullanıcı yazmadıysa false).</summary>
    public bool HasTranscript { get; set; }
}

/// <summary>Ekin metin parçası: PDF'te bir sayfa, seste bir bölüm. Ek yeniden işlenince parçalar baştan yazılır.</summary>
public sealed class AttachmentSegment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid AttachmentId { get; set; }

    /// <summary>Sıra (0'dan).</summary>
    public int Index { get; set; }

    /// <summary>PDF sayfa numarası (1'den).</summary>
    public int? Page { get; set; }

    /// <summary>Sesli notta bölümün başladığı saniye.</summary>
    public double? StartSeconds { get; set; }

    public string Text { get; set; } = "";
}
