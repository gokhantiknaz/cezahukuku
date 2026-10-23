using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Api.Admin;

/// <param name="Text">Kanunun tam metni (mevzuat.gov.tr web görünümünden kopyalanmış biçim).</param>
/// <param name="JoinWrappedLines">Metin PDF'ten kopyalandıysa cümle ortasında kırılan satırları birleştir.</param>
/// <param name="ValidFrom">İçe aktarılan metnin yürürlük başlangıcı (maddelerin ilk sürümleri bu tarihle açılır).</param>
/// <param name="ApplyMetadata">Künyeden okunan kabul ve Resmî Gazete bilgileri kanunda boşsa doldurulsun.</param>
public sealed record ImportLawTextRequest(string? Text, bool JoinWrappedLines, DateOnly? ValidFrom, bool ApplyMetadata);

public sealed record LawImportMetadata(string? Number, DateOnly? AcceptedOn, DateOnly? GazetteDate, string? GazetteNumber);

/// <param name="SortOrder">Aynı üst öğedeki düğüm ve maddeler arasında metindeki sırası (kayıtta aynen kullanılır).</param>
public sealed record ImportNodePreview(
    LawNodeKind Kind,
    string Number,
    string Title,
    int SortOrder,
    List<ImportNodePreview> Children,
    List<ImportArticlePreview> Articles);

/// <param name="ValidFrom">Kaydedilecek ilk sürümün başlangıcı: girilen yürürlük tarihi, maddede daha yeni bir
/// değişiklik notu varsa o notun tarihi (yapıştırılan metin kanunun güncel hâlidir; değişmiş maddenin bu metni
/// değişiklikten önce geçerli değildi).</param>
/// <param name="AmendedBy">Maddedeki en yeni değişiklik notu: "Değişik: 12/5/2022-7406/2 md.".</param>
public sealed record ImportArticlePreview(
    string Number,
    string Slug,
    string Title,
    bool IsRepealed,
    int SortOrder,
    DateOnly? ValidFrom,
    string? AmendedBy,
    List<ParagraphDto> Paragraphs);

/// <param name="Blockers">Varsa içe aktarma yapılamaz (ör. metinde madde yok).</param>
/// <param name="Warnings">İçe aktarmayı engellemeyen ama kontrol edilmesi gereken durumlar.</param>
/// <param name="Update">Kanunda zaten içerik varsa güncelleme özeti (hangi maddeler değişecek); boş kanunda null.</param>
public sealed record LawImportPreview(
    LawImportMetadata Metadata,
    List<ImportNodePreview> Nodes,
    List<ImportArticlePreview> Articles,
    int NodeCount,
    int ArticleCount,
    int RepealedCount,
    int FootnoteCount,
    List<string> Warnings,
    List<string> Blockers,
    LawUpdatePreview? Update);

/// <param name="Changes">Eklenecek ya da değişecek maddeler; değişmeyenler yalnız sayılır.</param>
/// <param name="NotInText">Kanunda olup yapıştırılan metinde bulunmayan maddeler (silinmez, kontrol için listelenir).</param>
public sealed record LawUpdatePreview(
    List<ArticleChange> Changes,
    int UnchangedCount,
    int NewNodeCount,
    int RenamedNodeCount,
    List<string> NotInText);

/// <param name="AsNewVersion">Metin değişikliği yeni sürüm olarak eklenir (eski metin önceki tarihler için kalır);
/// false ise yürürlükteki sürüm yerinde düzeltilir (değişiklik notu ya da daha yeni bir tarih yok).</param>
/// <param name="ValidFrom">Yeni sürümün başlangıcı; düzeltmede mevcut sürümün başlangıcı.</param>
public sealed record ArticleChange(
    string Number,
    string Slug,
    string Title,
    string? OldTitle,
    bool IsNew,
    bool TextChanged,
    bool AsNewVersion,
    bool Moved,
    DateOnly? ValidFrom,
    string? AmendedBy,
    List<ParagraphDto> OldParagraphs,
    List<ParagraphDto> NewParagraphs);

/// <param name="NodeCount">Eklenen düğüm sayısı.</param>
/// <param name="ArticleCount">Eklenen (boş kanunda) ya da eklenen ve değişen (güncellemede) madde sayısı.</param>
public sealed record LawImportResult(bool IsUpdate, int NodeCount, int ArticleCount, List<string> Warnings);
