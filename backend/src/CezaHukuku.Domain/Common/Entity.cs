namespace CezaHukuku.Domain.Common;

/// <summary>
/// Tüm kalıcı varlıkların ortak tabanı: UUID v7 kimlik, zaman damgaları ve soft delete.
/// Zaman damgaları SaveChanges sırasında altyapı katmanınca doldurulur.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt is not null;
}
