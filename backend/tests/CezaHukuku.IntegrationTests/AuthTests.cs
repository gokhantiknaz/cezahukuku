using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Auth;
using CezaHukuku.Domain.Users;

namespace CezaHukuku.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task SeededAdmin_SignsIn_AndSeesOwnRoles()
    {
        var admin = await factory.CreateAdminClientAsync();

        var me = await (await admin.GetAsync("/api/auth/me")).ReadAsync<CurrentUserResponse>();
        Assert.Equal(ApiFactory.AdminEmail, me.Email);
        Assert.Equal([Roles.SuperAdmin], me.Roles);
    }

    [Fact]
    public async Task WrongPassword_Returns401()
    {
        await factory.CreateAnonymousClient()
            .PostJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, "yanlis-sifre-123"))
            .ExpectAsync(401);
    }

    [Fact]
    public async Task Me_WithoutSession_Returns401()
    {
        await factory.CreateAnonymousClient().GetAsync("/api/auth/me").ExpectAsync(401);
    }

    [Fact]
    public async Task Registration_IsClosedByDefault()
    {
        await factory.CreateAnonymousClient()
            .PostJsonAsync("/api/auth/register", new RegisterRequest("yeni@test.local", "Sifre123456", "Yeni"))
            .ExpectAsync(404);
    }

    [Fact]
    public async Task DeactivatedUser_CannotSignIn()
    {
        var admin = await factory.CreateAdminClientAsync();
        var email = $"pasif{Guid.NewGuid():N}@test.local";
        const string password = "TestKullanici123";
        var user = await (await admin.PostJsonAsync("/api/admin/users",
                new CreateUserRequest(email, "Pasif Kullanıcı", Roles.Standard, password)))
            .ReadAsync<UserSummary>(201);

        await admin.PutJsonAsync($"/api/admin/users/{user.Id}", new UpdateUserRequest(user.DisplayName, Roles.Standard, false))
            .ExpectAsync(200);

        await factory.CreateAnonymousClient()
            .PostJsonAsync("/api/auth/login", new LoginRequest(email, password))
            .ExpectAsync(401);
    }

    [Fact]
    public async Task Admin_CannotDeactivateThemselves()
    {
        var admin = await factory.CreateAdminClientAsync();
        var me = await (await admin.GetAsync("/api/auth/me")).ReadAsync<CurrentUserResponse>();

        await admin.PutJsonAsync($"/api/admin/users/{me.Id}", new UpdateUserRequest(me.DisplayName, Roles.SuperAdmin, false))
            .ExpectAsync(400);
    }
}
