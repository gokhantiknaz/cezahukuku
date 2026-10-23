using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Api.Library;

public sealed record LibraryResponse(List<LawGroupWithLaws> Groups);

public sealed record LawGroupSummary(Guid Id, string Slug, string Title, int SortOrder);

public sealed record LawGroupWithLaws(Guid Id, string Slug, string Title, int SortOrder, List<LawSummary> Laws);

public sealed record LawSummary(
    Guid Id,
    Guid GroupId,
    string Slug,
    string Number,
    string? ShortName,
    string Name,
    DateOnly? AcceptedOn,
    DateOnly? GazetteDate,
    string? GazetteNumber,
    Visibility Visibility,
    int SortOrder);

/// <summary>Kanunun ağacı: kök düğümler ve hiçbir düğüme bağlı olmayan maddeler.</summary>
/// <param name="NoteKinds">Kanun düzeyinde okunabilir notların türleri (sol paneldeki Genel Gerekçe / Ders Notları).</param>
public sealed record LawTreeResponse(
    LawSummary Law,
    List<LawNodeTree> Nodes,
    List<ArticleSummary> Articles,
    List<EditorialNoteKind> NoteKinds);

/// <param name="Html">Sunucuda temizlenmiş HTML (izin verilen etiketler); olduğu gibi gösterilebilir.</param>
public sealed record EditorialNoteDto(
    Guid Id,
    EditorialNoteKind Kind,
    string? Title,
    string Html,
    Visibility Visibility,
    Visibility EffectiveVisibility,
    int SortOrder,
    DateTimeOffset UpdatedAt);

/// <param name="Visibility">Öğenin kendi ayarı (yönetim ekranı için).</param>
/// <param name="EffectiveVisibility">Üst öğelerle birlikte geçerli görünürlük.</param>
public sealed record LawNodeTree(
    Guid Id,
    Guid? ParentId,
    LawNodeKind Kind,
    string Number,
    string Title,
    int SortOrder,
    Visibility Visibility,
    Visibility EffectiveVisibility,
    List<LawNodeTree> Children,
    List<ArticleSummary> Articles);

public sealed record LawNodeSummary(
    Guid Id,
    Guid LawId,
    Guid? ParentId,
    LawNodeKind Kind,
    string Number,
    string Title,
    int SortOrder,
    Visibility Visibility);

public sealed record ArticleSummary(
    Guid Id,
    Guid? NodeId,
    string Slug,
    string Number,
    string Title,
    int SortOrder,
    Visibility Visibility,
    Visibility EffectiveVisibility);

public sealed record LawNodeRef(Guid Id, LawNodeKind Kind, string Number, string Title);

public sealed record ArticleRef(string Slug, string Number, string Title);

/// <param name="Path">Kökten maddenin bağlı olduğu düğüme kadar (breadcrumb).</param>
/// <param name="Version">Gösterilen sürüm; istenen tarihte yürürlükte sürüm yoksa ya da metin girilmediyse null.</param>
/// <param name="Versions">Tüm sürümler, en yeni önce (değişiklik tarihçesi).</param>
public sealed record ArticleDetailResponse(
    ArticleSummary Article,
    LawSummary Law,
    List<LawNodeRef> Path,
    ArticleVersionDetail? Version,
    List<ArticleVersionSummary> Versions,
    ArticleRef? Previous,
    ArticleRef? Next,
    List<EditorialNoteDto> Notes,
    List<DecisionDto> Decisions);

/// <param name="Html">Kararın tam metni ya da ilgili kısmı (sunucuda temizlenmiş HTML); boş olabilir.</param>
/// <param name="EffectiveVisibility">Maddenin altında gösterilirken maddenin görünürlüğüyle daraltılmış hâli.</param>
public sealed record DecisionDto(
    Guid Id,
    Court Court,
    string? Chamber,
    DecisionKind Kind,
    string? CaseNumber,
    string? DecisionNumber,
    DateOnly? DecisionDate,
    NormReviewOutcome? Outcome,
    DateOnly? GazetteDate,
    string? GazetteNumber,
    string Summary,
    string Html,
    string? SourceUrl,
    Visibility Visibility,
    Visibility EffectiveVisibility);

public sealed record ArticleVersionSummary(Guid Id, DateOnly? ValidFrom, DateOnly? ValidTo, string? AmendedBy, bool IsRepealed);

public sealed record ArticleVersionDetail(
    Guid Id,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string? AmendedBy,
    DateOnly? GazetteDate,
    string? GazetteNumber,
    bool IsRepealed,
    List<ParagraphDto> Paragraphs);

public sealed record ParagraphDto(Guid Id, int? Number, string Text, List<ParagraphItemDto> Items);

/// <param name="Level">1 bent, 2 alt bent, 0 bentlerden sonra gelen bağlayıcı metin.</param>
public sealed record ParagraphItemDto(Guid Id, int Level, string? Label, string Text);
