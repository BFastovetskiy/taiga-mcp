using TaigaMcp.Configuration;

namespace TaigaMcp.Taiga;

/// <summary>
/// Хранилище токенов Taiga, регистрируемое как singleton.
///
/// Экземпляр <see cref="TaigaClient"/> создаётся на каждый вызов инструмента (transient),
/// поэтому кэш токена <b>должен</b> жить вне него: иначе при каждом обращении
/// выполняется новый вход в Taiga (лишняя нагрузка, шум в аудите, риск блокировок).
///
/// Доступ к токенам потокобезопасен: чтение — <see cref="Volatile.Read{T}(ref T)"/>,
/// запись — <see cref="Interlocked.Exchange{T}(ref T, T)"/>. Это гарантирует видимость
/// изменений между потоками и атомарную замену ссылки без дополнительных блокировок.
/// </summary>
public sealed class TaigaAuthStore : IDisposable
{
    private string? _authToken;
    private string? _refreshToken;
    private bool _disposed;

    public TaigaAuthStore(TaigaOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            AuthToken = options.Token;
        }

        RefreshToken = options.RefreshToken;
    }

    /// <summary>Сериализует одновременные попытки входа/обновления токена.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>Текущий токен доступа (auth_token).</summary>
    public string? AuthToken
    {
        get => Volatile.Read(ref _authToken);
        set => Interlocked.Exchange(ref _authToken, value);
    }

    /// <summary>Текущий refresh-токен.</summary>
    public string? RefreshToken
    {
        get => Volatile.Read(ref _refreshToken);
        set => Interlocked.Exchange(ref _refreshToken, value);
    }

    /// <summary>Освобождает семафор. Вызывается контейнером DI при остановке приложения.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Gate.Dispose();
    }
}
