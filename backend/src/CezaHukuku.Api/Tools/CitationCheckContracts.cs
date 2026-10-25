using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Decisions;
using CezaHukuku.Parsing.Decisions;

namespace CezaHukuku.Api.Tools;

/// <param name="ReferenceDate">Olay (suç) tarihi: verilirse maddeler o tarihteki metne göre denetlenir.</param>
public sealed record CitationCheckRequest(string? Text, DateOnly? ReferenceDate);

/// <param name="Url">Bulguyla ilgili sayfa: maddenin o tarihteki metni, Değişiklikler sekmesi, resmî karar bankası…</param>
public sealed record CitationIssue(string Code, CheckSeverity Severity, string Message, string? Url);

/// <summary>Metindeki madde atıfı ve bulguları. Aynı atıfın tekrarları aynı <paramref name="Key"/> ile gelir.</summary>
/// <param name="Label">Kütüphanedeki maddeyse "TCK m. 53/1-c", değilse metinde yazıldığı gibi.</param>
/// <param name="Severity">Bulguların en ağırı.</param>
public sealed record CheckedArticleCitation(
    int Start,
    int Length,
    string Key,
    string Label,
    LinkedArticleRef? Article,
    int? Paragraph,
    string? Item,
    CheckSeverity Severity,
    List<CitationIssue> Issues);

/// <summary>Künyeyle eşleşen (ya da ona benzeyen) kütüphanedeki karar.</summary>
/// <param name="Url">Kararın göründüğü madde sekmesi; karar görünür bir maddeye bağlı değilse null.</param>
public sealed record LinkedDecisionRef(
    Guid Id,
    Court Court,
    string? Chamber,
    DecisionKind Kind,
    string? CaseNumber,
    string? DecisionNumber,
    DateOnly? DecisionDate,
    string? Url,
    string? SourceUrl);

/// <summary>Metindeki karar künyesi: ayrıştırılan parçalar, kütüphanedeki karşılığı ve bulgular.</summary>
/// <param name="Label">Künye tek biçimde: "Yargıtay 1. Ceza Dairesi, 12.03.2019, E. 2018/1234, K. 2019/567".</param>
/// <param name="CaseNumber">Esas numarası; bireysel başvuruda başvuru numarası.</param>
public sealed record CheckedDecisionCitation(
    int Start,
    int Length,
    string Key,
    string Label,
    Court? Court,
    string? Chamber,
    string? CaseNumber,
    string? DecisionNumber,
    bool IsApplication,
    DateOnly? DecisionDate,
    LinkedDecisionRef? Match,
    CheckSeverity Severity,
    List<CitationIssue> Issues);

public sealed record CitationCheckResult(List<CheckedArticleCitation> Articles, List<CheckedDecisionCitation> Decisions);

public sealed record ExtractedText(string Text);
