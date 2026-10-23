using System.Security.Claims;
using CezaHukuku.Api.Auth;
using CezaHukuku.Domain.Users;
using CezaHukuku.Infrastructure.Identity;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <summary>Kayıt kapalı olduğundan kullanıcıları yalnız super_admin açar ve yönetir.</summary>
public static class AdminUserEndpoints
{
    public static RouteGroupBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/users")
            .WithTags("AdminUsers")
            .RequireAuthorization(Policies.AdminOnly);

        group.MapGet("/", ListAsync).WithName("ListUsers");
        group.MapPost("/", CreateAsync).WithName("CreateUser");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateUser");
        group.MapPost("/{id:guid}/reset-password", ResetPasswordAsync).WithName("ResetUserPassword");

        return group;
    }

    private static async Task<Ok<List<UserSummary>>> ListAsync(AppDbContext db)
    {
        // Her kullanıcının tek rolü vardır; rol adı user_roles üzerinden tek sorguda alınır.
        var users = await (
            from u in db.Users
            let role = (from ur in db.UserRoles
                        join r in db.Roles on ur.RoleId equals r.Id
                        where ur.UserId == u.Id
                        select r.Name).FirstOrDefault()
            orderby u.DisplayName
            select new UserSummary(u.Id, u.Email!, u.DisplayName, role ?? "", u.IsActive, u.CreatedAt)
        ).ToListAsync();

        return TypedResults.Ok(users);
    }

    private static async Task<Results<Created<UserSummary>, ValidationProblem>> CreateAsync(
        CreateUserRequest request, UserManager<ApplicationUser> userManager)
    {
        if (!Roles.IsValid(request.Role))
            return InvalidRole();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName.Trim(),
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return TypedResults.ValidationProblem(created.Errors.ToValidationDictionary());

        await userManager.AddToRoleAsync(user, request.Role);

        var summary = new UserSummary(user.Id, user.Email!, user.DisplayName, request.Role, user.IsActive, user.CreatedAt);
        return TypedResults.Created($"/api/admin/users/{user.Id}", summary);
    }

    private static async Task<Results<Ok<UserSummary>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, UpdateUserRequest request, ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        if (!Roles.IsValid(request.Role))
            return InvalidRole();

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return TypedResults.NotFound();

        // Yönetici kendini pasifleştirip ya da yetkisini düşürüp sistemi yöneticisiz bırakamasın.
        if (userManager.GetUserId(principal) == user.Id.ToString()
            && (!request.IsActive || request.Role != Roles.SuperAdmin))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["self"] = ["Kendi hesabınızı pasifleştiremez veya yönetici rolünü kaldıramazsınız."],
            });
        }

        var deactivated = user.IsActive && !request.IsActive;
        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        await userManager.UpdateAsync(user);

        var currentRoles = await userManager.GetRolesAsync(user);
        var roleChanged = !currentRoles.SequenceEqual([request.Role]);
        if (roleChanged)
        {
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, request.Role);
        }

        // Rol değişikliği ya da pasifleştirme açık oturumlara da yansısın.
        if (deactivated || roleChanged)
            await userManager.UpdateSecurityStampAsync(user);

        return TypedResults.Ok(new UserSummary(user.Id, user.Email!, user.DisplayName, request.Role, user.IsActive, user.CreatedAt));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> ResetPasswordAsync(
        Guid id, ResetPasswordRequest request, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return TypedResults.NotFound();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!reset.Succeeded)
            return TypedResults.ValidationProblem(reset.Errors.ToValidationDictionary());

        return TypedResults.NoContent();
    }

    private static ValidationProblem InvalidRole() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["role"] = [$"Geçerli roller: {string.Join(", ", Roles.All)}"],
        });
}
