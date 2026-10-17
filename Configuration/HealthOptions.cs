namespace TaigaMcp.Configuration;

/// <summary>
/// Настройки проверок состояния (секция "Health").
/// Переопределяются переменной окружения HEALTH_READY_CACHE_SECONDS.
/// </summary>
public sealed class HealthOptions
{
    /// <summary>
    /// Время кэширования результата проверки готовности (readiness) в секундах.
    /// Реальный запрос к Taiga выполняется не чаще одного раза за этот интервал,
    /// а частые опросы (например, каждые 5 секунд) получают сохранённый результат.
    /// 0 — проверять доступность при каждом запросе.
    /// </summary>
    public int ReadyCacheSeconds { get; set; } = 60;
}
