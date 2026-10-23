namespace CezaHukuku.Domain.Laws;

public enum CitationTargetKind
{
    /// <summary>Kaynak maddenin kendi kanunu.</summary>
    SameLaw,

    /// <summary>Kanun numarasıyla: <see cref="ArticleCitation.TargetLaw"/> = "5271".</summary>
    LawNumber,

    /// <summary>Kanun adıyla: <see cref="ArticleCitation.TargetLaw"/> = aksansız küçük harfli ad ("ceza muhakemesi kanunu").</summary>
    LawName,
}

/// <summary>
/// Madde metnindeki atıf ("82 nci madde", "5271 sayılı Kanunun 231 inci maddesi"); sürüm kaydedilirken metinden
/// çıkarılır. Hedef kanun numara ya da adla tutulur, okuma anında çözülür: atıf yapılan kanun kütüphaneye sonradan
/// eklenince bağlantı kendiliğinden kurulur. "Bu maddeye atıf yapan maddeler" bu tablodan bulunur.
/// </summary>
public sealed class ArticleCitation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SourceVersionId { get; set; }
    public Guid SourceArticleId { get; set; }

    /// <summary>Atıfın geçtiği fıkra; bentteyse <see cref="ItemId"/> da dolu.</summary>
    public Guid ParagraphId { get; set; }
    public Guid? ItemId { get; set; }

    /// <summary>Fıkra ya da bent metnindeki konum (bağlantı yapılacak kısım).</summary>
    public int Start { get; set; }
    public int Length { get; set; }

    public CitationTargetKind TargetKind { get; set; }
    public string? TargetLaw { get; set; }

    /// <summary>Hedef maddenin adres parçası: "madde-82", "gecici-madde-1".</summary>
    public required string TargetArticleSlug { get; set; }
}
