namespace CezaHukuku.Api.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>
    /// Dışarıdan kayıt. Faz 1'de kapalı (kullanıcıyı admin açar); açılınca /api/auth/register
    /// standart rolle kullanıcı oluşturur.
    /// </summary>
    public bool AllowRegistration { get; set; }

    /// <summary>"Beni hatırla" seçilmeden açılan oturum bu kadar dakika işlem yapılmazsa kapanır (en az 10).</summary>
    public int IdleTimeoutMinutes { get; set; } = 60;

    /// <summary>"Beni hatırla" ile açılan oturum bu kadar gün işlem yapılmazsa kapanır (en az 1).</summary>
    public int RememberMeDays { get; set; } = 14;
}

public static class Policies
{
    public const string AdminOnly = "AdminOnly";

    /// <summary>Kişisel katman (vurgu, not): super_admin ve standard; reader yalnız okur.</summary>
    public const string PersonalLayer = "PersonalLayer";
}
