using System.Security.Claims;
using CezaHukuku.Domain.Users;
using CezaHukuku.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CezaHukuku.Api.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync).WithName("Login").AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).WithName("Logout").RequireAuthorization();
        group.MapGet("/me", MeAsync).WithName("GetCurrentUser").RequireAuthorization();
        group.MapPost("/register", RegisterAsync).WithName("Register").AllowAnonymous();

        return group;
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var invalid = TypedResults.Problem("E-posta veya şifre hatalı.", statusCode: StatusCodes.Status401Unauthorized);

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return invalid;

        var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return TypedResults.Problem(
                "Çok fazla hatalı deneme. Hesap geçici olarak kilitlendi.", statusCode: StatusCodes.Status423Locked);
        }
        if (!result.Succeeded)
            return invalid;

        return TypedResults.Ok(await ToResponseAsync(user, userManager));
    }

    private static async Task<NoContent> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> MeAsync(
        ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.IsActive)
            return TypedResults.Unauthorized();

        return TypedResults.Ok(await ToResponseAsync(user, userManager));
    }

    private static async Task<Results<Ok<CurrentUserResponse>, NotFound, ValidationProblem>> RegisterAsync(
        RegisterRequest request,
        IOptions<AuthOptions> options,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        // Kayıt kapalıyken uç nokta yokmuş gibi davranır.
        if (!options.Value.AllowRegistration)
            return TypedResults.NotFound();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName.Trim(),
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return TypedResults.ValidationProblem(created.Errors.ToValidationDictionary());

        await userManager.AddToRoleAsync(user, Roles.Standard);
        await signInManager.SignInAsync(user, isPersistent: false);
        return TypedResults.Ok(await ToResponseAsync(user, userManager));
    }

    internal static async Task<CurrentUserResponse> ToResponseAsync(ApplicationUser user, UserManager<ApplicationUser> userManager)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new CurrentUserResponse(user.Id, user.Email!, user.DisplayName, [.. roles]);
    }

    internal static Dictionary<string, string[]> ToValidationDictionary(this IEnumerable<IdentityError> errors) =>
        errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
