namespace CezaHukuku.Domain.Users;

public static class Roles
{
    /// <summary>Mevzuat ve kullanıcı yönetimi.</summary>
    public const string SuperAdmin = "super_admin";

    /// <summary>Okur, arar; kişisel vurgu/not/sesli not oluşturur.</summary>
    public const string Standard = "standard";

    /// <summary>Yalnız okur; kişisel alan oluşturamaz.</summary>
    public const string Reader = "reader";

    public static readonly IReadOnlyList<string> All = [SuperAdmin, Standard, Reader];

    public static bool IsValid(string role) => All.Contains(role);
}
