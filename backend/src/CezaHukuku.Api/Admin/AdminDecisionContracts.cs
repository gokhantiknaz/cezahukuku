using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Api.Admin;

/// <param name="Outcome">Yalnız norm denetiminde: iptal, kısmen iptal, ret.</param>
/// <param name="Html">Tam metin ya da ilgili kısım (düzenleyiciden HTML); sunucuda temizlenir.</param>
/// <param name="ArticleIds">Oluştururken bağlanacak maddeler; güncellemede yok sayılır (bağlantılar ayrı yönetilir).</param>
public sealed record SaveDecisionRequest(
    Court Court,
    string? Chamber,
    DecisionKind Kind,
    string? CaseNumber,
    string? DecisionNumber,
    DateOnly? DecisionDate,
    NormReviewOutcome? Outcome,
    DateOnly? GazetteDate,
    string? GazetteNumber,
    string? Summary,
    string? Html,
    string? SourceUrl,
    Visibility Visibility,
    List<Guid>? ArticleIds);

/// <summary>Kararın bağlı olduğu madde (okuma ekranındaki adresiyle).</summary>
public sealed record LinkedArticle(Guid ArticleId, string LawSlug, string LawLabel, string ArticleSlug, string Number, string Title);

public sealed record AdminDecisionDetail(DecisionDto Decision, List<LinkedArticle> Articles);

public sealed record AdminDecisionListItem(DecisionDto Decision, int ArticleCount);

public sealed record AdminDecisionList(List<AdminDecisionListItem> Items, int Total);
