using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CezaHukuku.Api.Admin;
using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Api.Content;
using CezaHukuku.Api.Library;
using CezaHukuku.Api.Personal;
using CezaHukuku.Api.Search;
using CezaHukuku.Api.Tools;
using CezaHukuku.Domain.Users;
using CezaHukuku.Infrastructure;
using CezaHukuku.Infrastructure.Identity;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Infrastructure.Search;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration);

// Oturum çerezini şifreleyen anahtarlar: sunucuda kalıcı bir klasörde tutulur ki yeniden başlatınca oturumlar düşmesin.
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection()
        .SetApplicationName("cezahukuku")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.Section));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Web istemcisi Next.js üzerinden aynı origin'den gelir; kimlik doğrulama HttpOnly cookie ile.
// (Faz 2 mobil için bearer token şeması eklenecek.) Canlıda site yalnız HTTPS'tir: API'ye Next.js'ten iç ağda http ile
// gelinse de çerez Secure işaretlenir.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "ch_auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;

    // API olduğu için login sayfasına yönlendirme yerine durum kodu dön.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// Pasifleştirilen ya da rolü değişen kullanıcının açık oturumu en geç 1 dakikada düşer.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(Roles.SuperAdmin))
    .AddPolicy(Policies.PersonalLayer, policy => policy.RequireRole(Roles.SuperAdmin, Roles.Standard));

// Enum'lar API'de metin olarak taşınır ("public", "kitap"); web istemcisi bunları tip olarak alır.
// Sayılar yalnız sayı olarak kabul edilir; aksi hâlde OpenAPI şemasında "integer | string" görünür.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = RequestBodyErrors.AddFieldErrors);
// Okunamayan istek gövdesi (bozuk JSON, yanlış biçimde tarih) istemci hatasıdır. Minimal API bunu her ortamda
// istisna olarak fırlatsın ki hata yakalayıcı alan bazında hata içeren 400 dönsün (RequestBodyErrors); istemci
// hatası olduğundan sunucu günlüğüne "unhandled exception" olarak yazılmaz (istek satırı yine 400 ile görünür).
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.Configure<ExceptionHandlerOptions>(options =>
{
    options.StatusCodeSelector = exception =>
        exception is BadHttpRequestException badRequest ? badRequest.StatusCode : StatusCodes.Status500InternalServerError;
    options.SuppressDiagnosticsCallback = context => context.Exception is BadHttpRequestException;
});
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/api/health", () => TypedResults.Ok(new { status = "ok" })).WithName("Health").WithTags("System").AllowAnonymous();
app.MapAuthEndpoints();
app.MapAdminUserEndpoints();
app.MapLibraryEndpoints();
app.MapAdminLawEndpoints();
app.MapHighlightEndpoints();
app.MapSearchEndpoints();
app.MapNewsEndpoints();
app.MapQuizEndpoints();
app.MapCitationCheckEndpoints();

// Derleme sırasında OpenAPI belgesi üretilirken uygulama veritabanına dokunmadan başlatılır.
if (!IsBuildTimeOpenApiGeneration())
{
    await using var scope = app.Services.CreateAsyncScope();
    // Geliştirmede her zaman; canlıda Database:MigrateOnStartup=true ise (tek sunuculu kurulum).
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

    // Atıf tablosu sonradan eklendi: tablo boşken metni olan sürümler varsa (eski kayıtlar) bir kez taranır.
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!await db.ArticleCitations.AnyAsync() && await db.ArticleVersions.AnyAsync())
    {
        var count = await CitationIndex.RebuildAllAsync(db);
        app.Logger.LogInformation("Mevcut sürümler atıflar için tarandı: {Count} atıf bulundu.", count);
    }
    if (app.Environment.IsDevelopment())
        await scope.ServiceProvider.GetRequiredService<DemoLibrarySeeder>().SeedAsync();

    // Arama indeksi sonradan eklendi: indeks boşken içerik varsa bir kez baştan kurulur.
    if (!await db.SearchDocs.AnyAsync() && await db.Laws.AnyAsync())
    {
        var count = await SearchIndexer.RebuildAllAsync(db);
        app.Logger.LogInformation("Arama indeksi kuruldu: {Count} kayıt.", count);
    }
}

app.Run();

static bool IsBuildTimeOpenApiGeneration() =>
    Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

public partial class Program;
