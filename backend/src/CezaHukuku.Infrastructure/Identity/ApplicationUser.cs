using Microsoft.AspNetCore.Identity;

namespace CezaHukuku.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Pasif kullanıcı giriş yapamaz; mevcut oturumları güvenlik damgası yenilenerek düşürülür.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
