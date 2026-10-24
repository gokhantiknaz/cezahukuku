using CezaHukuku.Infrastructure.Attachments;
using CezaHukuku.Infrastructure.Files;
using CezaHukuku.Infrastructure.Identity;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CezaHukuku.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default tanımlı değil.");

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TimestampInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<TimestampInterceptor>())
            // Soft delete filtresi ilişkinin iki ucunda da var; EF'nin "filtreli zorunlu ilişki" uyarısı burada geçersiz.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        // Yüklenen dosyalar: yerel disk (üretimde S3 uyumlu depo eklenecek).
        var storageRoot = configuration.GetSection(StorageOptions.Section).Get<StorageOptions>()?.Root ?? new StorageOptions().Root;
        services.AddSingleton<IFileStorage>(sp => new LocalFileStorage(
            Path.IsPathRooted(storageRoot) ? storageRoot : Path.Combine(sp.GetRequiredService<IHostEnvironment>().ContentRootPath, storageRoot)));

        // Ekler (PDF, sesli not) arka planda işlenir; yazıya dökme ayarlıysa OpenAI uyumlu servise gider.
        services.Configure<TranscriptionOptions>(configuration.GetSection(TranscriptionOptions.Section));
        services.AddHttpClient<ITranscriber, OpenAiCompatibleTranscriber>(client => client.Timeout = TimeSpan.FromMinutes(5));
        services.AddSingleton<AttachmentQueue>();
        services.AddHostedService<AttachmentProcessor>();

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.Section));
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<DemoLibrarySeeder>();

        return services;
    }
}
