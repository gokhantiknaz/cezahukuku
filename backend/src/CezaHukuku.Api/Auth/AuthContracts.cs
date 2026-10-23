namespace CezaHukuku.Api.Auth;

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles);
