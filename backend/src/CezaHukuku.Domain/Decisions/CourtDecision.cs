using CezaHukuku.Domain.Common;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Domain.Decisions;

public enum Court
{
    Yargitay,
    AnayasaMahkemesi,
    Aihm,
    Danistay,
    BolgeAdliyeMahkemesi,
    UyusmazlikMahkemesi,
    Diger,
}

public enum DecisionKind
{
    /// <summary>Yargıtay, Danıştay, BAM, AİHM… kararları (İçtihatlar sekmesi).</summary>
    Ictihat,

    /// <summary>Anayasa Mahkemesinin norm denetimi kararı: iptal, ret (Norm Denetimi sekmesi).</summary>
    NormDenetimi,
}

public enum NormReviewOutcome
{
    Iptal,
    KismenIptal,
    Ret,
    Diger,
}

/// <summary>
/// Mahkeme kararı. Bir karar birden çok maddeye bağlanabilir (<see cref="ArticleDecision"/>); bir kez girilir, ilgili
/// maddelerin hepsinde görünür. Metin temizlenmiş HTML olarak saklanır, arama için düz metni de tutulur.
/// </summary>
public sealed class CourtDecision : Entity
{
    public Court Court { get; set; }

    /// <summary>Daire ya da kurul: "1. Ceza Dairesi", "Ceza Genel Kurulu", "Genel Kurul", "Büyük Daire".</summary>
    public string? Chamber { get; set; }

    public DecisionKind Kind { get; set; }

    /// <summary>Esas numarası ("2019/1234"); AİHM'de başvuru numarası.</summary>
    public string? CaseNumber { get; set; }

    /// <summary>Karar numarası ("2020/567").</summary>
    public string? DecisionNumber { get; set; }

    public DateOnly? DecisionDate { get; set; }

    /// <summary>Norm denetiminde sonuç; içtihatta null.</summary>
    public NormReviewOutcome? Outcome { get; set; }

    /// <summary>Kararın yayımlandığı Resmî Gazete (AYM kararlarında).</summary>
    public DateOnly? GazetteDate { get; set; }
    public string? GazetteNumber { get; set; }

    /// <summary>Kısa özet: listede ve aramada gösterilir.</summary>
    public string Summary { get; set; } = "";

    /// <summary>Tam metin ya da ilgili kısım (temizlenmiş HTML); boş olabilir.</summary>
    public string Html { get; set; } = "";

    public string PlainText { get; set; } = "";

    /// <summary>Kararın kaynağı (Yargıtay karar arama, AYM, HUDOC…).</summary>
    public string? SourceUrl { get; set; }

    public Visibility Visibility { get; set; }
}

/// <summary>Kararın bağlı olduğu madde. Bağlantı kaldırılınca satır silinir (karar kalır).</summary>
public sealed class ArticleDecision
{
    public Guid ArticleId { get; set; }
    public Guid DecisionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
