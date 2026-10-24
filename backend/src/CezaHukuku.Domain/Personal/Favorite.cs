namespace CezaHukuku.Domain.Personal;

/// <summary>Kullanıcının favori maddesi (yalnız sahibi görür). Kaldırılınca satır silinir.</summary>
public sealed class Favorite
{
    public Guid OwnerId { get; set; }
    public Guid ArticleId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
