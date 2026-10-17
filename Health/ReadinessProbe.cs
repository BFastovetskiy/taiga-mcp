using Microsoft.Extensions.DependencyInjection;
using TaigaMcp.Configuration;
using TaigaMcp.Taiga;

namespace TaigaMcp.Health;

/// <summary>
/// Кэширующий зонд готовности.
///
/// Раньше endpoint <c>/health/ready</c> выполнял реальный запрос <c>users/me</c> при каждом
/// опросе: при интервале 5 секунд это ~17 000 запросов в сутки к Taiga. Теперь результат
/// кэшируется, и повторные проверки не старше <see cref="HealthOptions.ReadyCacheSeconds"/>
/// возвращают сохранённое состояние, а реальный запрос выполняется не чаще одного раза
/// за интервал (с защитой от параллельных проверок).
/// </summary>
public sealed class ReadinessProbe : IDisposable
{
    private sealed record Snapshot(HealthResult Result, DateTimeOffset CheckedAt);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TaigaOptions _options;
    private readonly int _cacheSeconds;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Snapshot? _snapshot;
    private bool _disposed;

    public ReadinessProbe(IServiceScopeFactory scopeFactory, TaigaOptions options, HealthOptions health)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _cacheSeconds = Math.Max(0, health.ReadyCacheSeconds);
    }

    /// <summary>Возвращает состояние готовности, обновляя его не чаще заданного интервала.</summary>
    public async Task<HealthResult> GetAsync(CancellationToken ct = default)
    {
        if (TryGetFreshSnapshot(out var cached))
        {
            return cached;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (TryGetFreshSnapshot(out cached))
            {
                return cached;
            }

            // Клиент создаём через scope: так соблюдается жизненный цикл IHttpClientFactory.
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<TaigaClient>();
            var result = await HealthChecks.CheckReadyAsync(client, _options, ct).ConfigureAwait(false);

            Volatile.Write(ref _snapshot, new Snapshot(result, DateTimeOffset.UtcNow));
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryGetFreshSnapshot(out HealthResult result)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot is not null &&
            _cacheSeconds > 0 &&
            DateTimeOffset.UtcNow - snapshot.CheckedAt < TimeSpan.FromSeconds(_cacheSeconds))
        {
            result = snapshot.Result;
            return true;
        }

        result = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }
}
