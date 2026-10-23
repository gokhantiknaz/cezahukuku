using CezaHukuku.Domain.Common;

namespace CezaHukuku.Domain.Laws;

public sealed class Law : Entity
{
    public Guid GroupId { get; set; }

    /// <summary>Adres çubuğundaki kısa ad: "tck" → /tck/madde-81.</summary>
    public required string Slug { get; set; }

    /// <summary>Kanun numarası: "5237".</summary>
    public required string Number { get; set; }

    /// <summary>Kısaltma: "TCK". Her kanunun yaygın bir kısaltması yoktur.</summary>
    public string? ShortName { get; set; }

    public required string Name { get; set; }

    /// <summary>Kabul tarihi.</summary>
    public DateOnly? AcceptedOn { get; set; }

    /// <summary>Resmî Gazete tarihi.</summary>
    public DateOnly? GazetteDate { get; set; }

    /// <summary>Resmî Gazete sayısı.</summary>
    public string? GazetteNumber { get; set; }

    public Visibility Visibility { get; set; }
    public int SortOrder { get; set; }
}
