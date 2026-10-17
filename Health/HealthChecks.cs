using TaigaMcp.Configuration;
using TaigaMcp.Taiga;

namespace TaigaMcp.Health;

/// <summary>Результат проверки готовности.</summary>
public readonly record struct HealthResult(bool IsReady, string? Reason)
{
    public static HealthResult Ready() => new(true, null);

    public static HealthResult NotReady(string reason) => new(false, reason);
}

/// <summary>Логика проверок состояния сервера (вынесена отдельно для тестируемости).</summary>
public static class HealthChecks
{
    /// <summary>
    /// Проверка готовности (readiness): конфигурация Taiga корректна и доступ к API
    /// с текущими учётными данными работает. Использует лёгкий запрос users/me.
    /// </summary>
    public static async Task<HealthResult> CheckReadyAsync(
        TaigaClient client,
        TaigaOptions options,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl) ||
            !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return HealthResult.NotReady("BaseUrl не задан или имеет некорректный формат.");
        }

        try
        {
            await client.GetAsync("users/me", disablePagination: null, ct: ct).ConfigureAwait(false);
            return HealthResult.Ready();
        }
        catch (Exception ex)
        {
            return HealthResult.NotReady(ex.Message);
        }
    }
}
