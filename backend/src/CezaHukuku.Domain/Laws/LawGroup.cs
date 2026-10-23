using CezaHukuku.Domain.Common;

namespace CezaHukuku.Domain.Laws;

/// <summary>Kütüphanedeki kanun grubu: Temel Ceza Kanunları, Özel Ceza Kanunları, Kabahatler vb.</summary>
public sealed class LawGroup : Entity
{
    public required string Slug { get; set; }
    public required string Title { get; set; }
    public int SortOrder { get; set; }
}
