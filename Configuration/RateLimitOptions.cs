namespace TaigaMcp.Configuration;

/// <summary>
/// Настройки ограничения частоты запросов к MCP-эндпоинту (rate limiting).
/// Берутся из секции "RateLimit" и могут быть переопределены переменными
/// RATE_LIMIT_ENABLED / RATE_LIMIT_PERMIT / RATE_LIMIT_WINDOW_SECONDS.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>Включено ли ограничение частоты запросов.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Максимальное число запросов в окне на один ключ (токен или IP).</summary>
    public int PermitLimit { get; set; } = 120;

    /// <summary>Размер окна в секундах.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Максимальная длина очереди ожидающих запросов сверх лимита.
    /// 0 — лишние запросы сразу отклоняются (429).
    /// </summary>
    public int QueueLimit { get; set; }
}
