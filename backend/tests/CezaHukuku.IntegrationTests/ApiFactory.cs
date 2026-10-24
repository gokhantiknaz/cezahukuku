using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CezaHukuku.Api.Auth;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CezaHukuku.IntegrationTests;

/// <summary>
/// Gerçek API'yi ayrı bir test veritabanına karşı çalıştırır. Veritabanı her test oturumunda silinip
/// migration'larla yeniden kurulur; testler birbirini etkilemesin diye her biri kendi kanununu açar.
/// Bağlantı: CEZAHUKUKU_TEST_CONNECTION ortam değişkeni, yoksa API'nin geliştirme bağlantısının (appsettings +
/// user-secrets) sunucusunda sabit adlı <see cref="DatabaseName"/> veritabanı. Ad, elle deneme yapılan
/// geliştirme veritabanından bilerek ayrıdır; o veritabanı testlerde silinmesin.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "TestYonetici123";
    public const string DatabaseName = "cezahukuku_integration";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string connectionString = ResolveConnectionString();

    public static readonly string StorageRoot = Path.Combine(Path.GetTempPath(), "cezahukuku-tests", Guid.NewGuid().ToString("N"));

    public ApiFactory()
    {
        // Program yapılandırmayı WebApplicationFactory geçersiz kılmalarından önce okuduğundan ayarlar ortam
        // değişkeniyle verilir (appsettings.Development.json'dan sonra yüklenir ve onu ezer).
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);
        Environment.SetEnvironmentVariable("Seed__AdminEmail", AdminEmail);
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPassword);
        Environment.SetEnvironmentVariable("Seed__DemoLibrary", "false");
        // Yüklenen dosyalar her test oturumunda geçici klasöre.
        Environment.SetEnvironmentVariable("Storage__Root", StorageRoot);
        Environment.SetEnvironmentVariable("Transcription__BaseUrl", "");
    }

    // Geliştirme ortamında Program migration'ları uygular ve ilk yöneticiyi açar.
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public HttpClient CreateAnonymousClient() => CreateClient();

    public async Task<HttpClient> CreateSignedInClientAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);
        await response.ReadAsync<CurrentUserResponse>();
        return client;
    }

    public Task<HttpClient> CreateAdminClientAsync() => CreateSignedInClientAsync(AdminEmail, AdminPassword);

    private static string ResolveConnectionString()
    {
        if (Environment.GetEnvironmentVariable("CEZAHUKUKU_TEST_CONNECTION") is { Length: > 0 } fromEnv)
            return fromEnv;

        var apiDirectory = FindApiDirectory();
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(apiDirectory, "appsettings.json"))
            .AddJsonFile(Path.Combine(apiDirectory, "appsettings.Development.json"), optional: true)
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .Build();
        var builder = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("Default"));
        builder.Database = DatabaseName;
        return builder.ConnectionString;
    }

    private static string FindApiDirectory()
    {
        var directory = typeof(ApiFactory).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "ApiProjectDirectory")?.Value;
        return directory is not null && Directory.Exists(directory)
            ? Path.GetFullPath(directory)
            : throw new InvalidOperationException("CezaHukuku.Api klasörü bulunamadı; CEZAHUKUKU_TEST_CONNECTION ayarlayın.");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

internal static class HttpTestExtensions
{
    /// <summary>Beklenen durum kodunu doğrular (hatada yanıt gövdesini gösterir) ve gövdeyi okur.</summary>
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, int expectedStatus = 200)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == expectedStatus,
            $"Beklenen {expectedStatus}, gelen {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, ApiFactory.Json)!;
    }

    public static async Task ExpectAsync(this Task<HttpResponseMessage> request, int expectedStatus)
    {
        var response = await request;
        var body = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == expectedStatus,
            $"Beklenen {expectedStatus}, gelen {(int)response.StatusCode}: {body}");
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, ApiFactory.Json);
}
