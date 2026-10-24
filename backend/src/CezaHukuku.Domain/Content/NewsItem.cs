using CezaHukuku.Domain.Common;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Domain.Content;

/// <summary>
/// Ceza Gündemi haberi: alttaki şeritte ve gündem sayfasında görünür (kanun değişikliği, önemli karar, duyuru).
/// Yönetici girer; bağlantı verilirse habere tıklanınca kaynağa gidilir.
/// </summary>
public sealed class NewsItem : Entity
{
    public const int MaxTitleLength = 300;
    public const int MaxSummaryLength = 2000;

    public string Title { get; set; } = "";
    public string? Summary { get; set; }

    /// <summary>Kaynak (Resmî Gazete, karar metni…): http(s) adresi.</summary>
    public string? Url { get; set; }

    public DateOnly PublishedOn { get; set; }
    public Visibility Visibility { get; set; }
}
