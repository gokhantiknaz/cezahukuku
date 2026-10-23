namespace CezaHukuku.Api.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>
    /// Dışarıdan kayıt. Faz 1'de kapalı (kullanıcıyı admin açar); açılınca /api/auth/register
    /// standart rolle kullanıcı oluşturur.
    /// </summary>
    public bool AllowRegistration { get; set; }
}

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
}
