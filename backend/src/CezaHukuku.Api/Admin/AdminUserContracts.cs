namespace CezaHukuku.Api.Admin;

public sealed record UserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateUserRequest(string Email, string DisplayName, string Role, string Password);

public sealed record UpdateUserRequest(string DisplayName, string Role, bool IsActive);

public sealed record ResetPasswordRequest(string NewPassword);
