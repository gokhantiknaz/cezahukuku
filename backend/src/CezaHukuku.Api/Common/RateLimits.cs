using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CezaHukuku.Api.Common;

/// <summary>İstemci (IP) başına dakikalık istek sınırları (RateLimit bölümü).</summary>
public sealed class RateLimitOptions
{
    public const string Section = "RateLimit";

    /// <summary>Giriş denemesi. Hesap kilidi tek hesabı korur; bu sınır çok hesaba dağıtılan denemeleri yavaşlatır.</summary>
    public int LoginPerMinute { get; set; } = 10;

    /// <summary>Herkese açık atıf denetimi ve dosyadan metin alma (uzun metin işler).</summary>
    public int ToolsPerMinute { get; set; } = 20;
}

/// <summary>
/// Uç noktalara <c>RequireRateLimiting(RateLimits.Login)</c> ile bağlanan sınırlar. İstemci adresi canlıda Caddy'nin
/// X-Forwarded-For başlığından gelir (Program: UseForwardedHeaders; yalnız yerel ağdaki vekile güvenilir).
/// </summary>
public static class RateLimits
{
    public const string Login = "login";
    public const string Tools = "tools";

    public static IServiceCollection AddRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.Section));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                await TypedResults.Problem("Çok fazla istek gönderildi. Lütfen biraz sonra tekrar deneyin.",
                    statusCode: StatusCodes.Status429TooManyRequests).ExecuteAsync(context.HttpContext);
            };
            options.AddPolicy(Login, context => PerClient(context, o => o.LoginPerMinute));
            options.AddPolicy(Tools, context => PerClient(context, o => o.ToolsPerMinute));
        });
        return services;
    }

    private static RateLimitPartition<string> PerClient(HttpContext context, Func<RateLimitOptions, int> limit)
    {
        var permits = Math.Max(1, limit(context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitOptions>>().CurrentValue));
        var client = context.Connection.RemoteIpAddress?.ToString() ?? "";
        return RateLimitPartition.GetFixedWindowLimiter($"{client}|{permits}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    }
}
