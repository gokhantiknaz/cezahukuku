using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Domain.Users;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task Session_ClosesAfterIdleTimeout_UnlessRememberMe()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        async Task<HttpClient> SignInAsync(bool rememberMe)
        {
            var client = app.CreateClient();
            await (await client.PostJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword, rememberMe)))
                .ReadAsync<CurrentUserResponse>();
            return client;
        }
        var session = await SignInAsync(rememberMe: false);
        var remembered = await SignInAsync(rememberMe: true);

        // Her işlem 60 dakikalık süreyi baştan başlatır (varsayılan kayan yenileme 20. dakikada yenilemezdi, oturum
        // 60. dakikada kapanırdı).
        clock.Advance(TimeSpan.FromMinutes(20));
        await session.GetAsync("/api/auth/me").ExpectAsync(200);
        clock.Advance(TimeSpan.FromMinutes(50));
        await session.GetAsync("/api/auth/me").ExpectAsync(200);

        clock.Advance(TimeSpan.FromMinutes(61));
        await session.GetAsync("/api/auth/me").ExpectAsync(401);
        await remembered.GetAsync("/api/auth/me").ExpectAsync(200);
    }

    [Fact]
    public async Task Login_IsRateLimitedPerClient()
    {
        await using var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.Configure<RateLimitOptions>(o => o.LoginPerMinute = 3)));
        var client = app.CreateClient();

        for (var i = 0; i < 3; i++)
            await client.PostJsonAsync("/api/auth/login", new LoginRequest($"yok{i}@test.local", "yanlis-sifre-123")).ExpectAsync(401);
        var limited = await client.PostJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword));

        Assert.Equal(429, (int)limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter is not null);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public void Advance(TimeSpan by) => now += by;

        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/api/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task WrongPassword_Returns401()
    {
        await factory.CreateAnonymousClient()
            .PostJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, "yanlis-sifre-123"))
            .ExpectAsync(401);
    }

    [Fact]
    public async Task MalformedJsonBody_Returns400()
    {
        var content = new StringContent("{\"email\": ", System.Text.Encoding.UTF8, "application/json");

        await factory.CreateAnonymousClient().PostAsync("/api/auth/login", content).ExpectAsync(400);
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
