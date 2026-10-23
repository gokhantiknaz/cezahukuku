using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Api.Admin;

// Kaydetme istekleri hem oluşturmada hem güncellemede kullanılır. SortOrder verilmezse oluştururken kardeşlerin
// sonuna eklenir, güncellerken mevcut sıra korunur. Slug verilmezse addan/numaradan türetilir.

public sealed record SaveLawGroupRequest(string Title, string? Slug, int? SortOrder);

public sealed record SaveLawRequest(
    Guid GroupId,
    string Number,
    string Name,
    string? ShortName,
    string? Slug,
    DateOnly? AcceptedOn,
    DateOnly? GazetteDate,
    string? GazetteNumber,
    Visibility Visibility,
    int? SortOrder);

/// <param name="ParentId">Üst düğüm; null ise kanunun doğrudan altına.</param>
public sealed record SaveLawNodeRequest(
    Guid? ParentId,
    LawNodeKind Kind,
    string Number,
    string Title,
    Visibility Visibility,
    int? SortOrder);

/// <param name="NodeId">Bağlanacağı düğüm; null ise kanunun doğrudan altına.</param>
public sealed record SaveArticleRequest(Guid? NodeId, string Number, string? Title, Visibility Visibility, int? SortOrder);

/// <param name="ValidTo">Boş bırakılırsa sürüm hâlen yürürlükte sayılır; o zamana kadar yürürlükte olan sürüm,
/// yeni sürümün başlangıcından bir gün önce kapatılır.</param>
public sealed record SaveArticleVersionRequest(
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string? AmendedBy,
    DateOnly? GazetteDate,
    string? GazetteNumber,
    bool IsRepealed,
    List<ParagraphInput> Paragraphs);

/// <param name="Id">Önceki sürümdeki aynı fıkranın kimliği; verilirse korunur (vurgu ve notlar fıkraya bağlı kalır).
/// Yeni fıkrada boş bırakılır.</param>
public sealed record ParagraphInput(Guid? Id, int? Number, string? Text, List<ParagraphItemInput>? Items);

/// <param name="Level">1 bent, 2 alt bent, 0 bentlerden sonra gelen bağlayıcı metin.</param>
public sealed record ParagraphItemInput(Guid? Id, int Level, string? Label, string Text);

/// <param name="Text">Mevzuat biçiminde madde metni: "(1) ...", "a) ...", "1. ...".</param>
/// <param name="BaseVersionId">Düzenlenen ya da yeni sürümün dayandığı sürüm; eşleşen fıkra ve bentler onun
/// kimliklerini alır.</param>
public sealed record ParseArticleTextRequest(string? Text, Guid? BaseVersionId);
