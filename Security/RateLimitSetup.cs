using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using TaigaMcp.Configuration;

namespace TaigaMcp.Security;

/// <summary>
/// Настройка ограничения частоты запросов к MCP-эндпоинту.
/// Ключ разделения (partition) — токен доступа (стабильный хэш) либо IP клиента.
/// </summary>
public static class RateLimitSetup
{
    /// <summary>Создаёт раздел лимитера для конкретного запроса.</summary>
    public static RateLimitPartition<string> CreatePartition(HttpContext context, RateLimitOptions options)
        => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ResolvePartitionKey(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = TimeSpan.FromSeconds(Math.Max(1, options.WindowSeconds)),
                QueueLimit = options.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            });

    /// <summary>
    /// Определяет ключ разделения: хэш токена доступа, если он передан,
    /// иначе строковый адрес клиента.
    /// </summary>
    public static string ResolvePartitionKey(HttpContext context)
    {
        var token = ExtractToken(context);
        if (!string.IsNullOrEmpty(token))
        {
            return "token:" + ShortHash(token);
        }

        var ip = context.Connection.RemoteIpAddress;
        return ip is null ? "unknown" : "ip:" + ip;
    }

    private static string? ExtractToken(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKey) &&
            !StringValues.IsNullOrEmpty(apiKey))
        {
            return apiKey.ToString();
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return null;
    }

    private static string ShortHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
}
