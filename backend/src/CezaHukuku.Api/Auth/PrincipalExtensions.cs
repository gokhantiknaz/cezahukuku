using System.Security.Claims;

namespace CezaHukuku.Api.Auth;

internal static class PrincipalExtensions
{
    /// <summary>
    /// Private içerik giriş yapmış her kullanıcıya açıktır (rolden bağımsız). Pasifleştirilen kullanıcının oturumu
    /// güvenlik damgası doğrulamasıyla en geç 1 dakikada düşer.
    /// </summary>
    public static bool CanSeePrivate(this ClaimsPrincipal principal) => principal.Identity?.IsAuthenticated == true;
}
