using CezaHukuku.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CezaHukuku.Infrastructure.Identity;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string AdminDisplayName { get; set; } = "Sistem Yöneticisi";

    /// <summary>Boş veritabanına örnek kütüphane kesiti eklensin (yalnız geliştirme).</summary>
    public bool DemoLibrary { get; set; }
}

/// <summary>
/// Rolleri oluşturur; hiç super_admin yoksa yapılandırmadaki bilgilerle ilk yöneticiyi açar.
/// Kayıt kapalı olduğu için sisteme ilk giriş bu kullanıcıyla yapılır.
/// </summary>
public sealed class IdentitySeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<SeedOptions> options,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() });
        }

        if ((await userManager.GetUsersInRoleAsync(Roles.SuperAdmin)).Count > 0)
            return;

        var seed = options.Value;
        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            logger.LogWarning(
                "Hiç super_admin yok ve Seed:AdminEmail / Seed:AdminPassword ayarlanmamış; ilk yönetici oluşturulmadı.");
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = seed.AdminEmail,
            Email = seed.AdminEmail,
            EmailConfirmed = true,
            DisplayName = seed.AdminDisplayName,
        };

        var created = await userManager.CreateAsync(admin, seed.AdminPassword);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                "İlk yönetici oluşturulamadı: " + string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(admin, Roles.SuperAdmin);
        logger.LogInformation("İlk yönetici oluşturuldu: {Email}", seed.AdminEmail);
    }
}
