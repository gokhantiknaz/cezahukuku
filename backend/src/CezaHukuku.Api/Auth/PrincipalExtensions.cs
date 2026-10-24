using System.Security.Claims;

namespace CezaHukuku.Api.Auth;

internal static class PrincipalExtensions
{
    /// <summary>
    /// Private içerik giriş yapmış her kullanıcıya açıktır (rolden bağımsız). Pasifleştirilen kullanıcının oturumu
    /// güvenlik damgası doğrulamasıyla en geç 1 dakikada düşer.
    /// </summary>
    public static bool CanSeePrivate(this ClaimsPrincipal principal) => principal.Identity?.IsAuthenticated == true;

    /// <summary>Giriş yapmış kullanıcının kimliği (yetki gerektiren uç noktalarda; anonimde çağrılmaz).</summary>
    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new InvalidOperationException("Oturumda kullanıcı kimliği yok."));
}
