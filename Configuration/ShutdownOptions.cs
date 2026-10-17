namespace TaigaMcp.Configuration;

/// <summary>
/// Настройки корректного завершения работы сервера.
/// Берутся из секции "Shutdown" и могут быть переопределены переменными
/// SHUTDOWN_TIMEOUT_SECONDS / SHUTDOWN_ON_UNHANDLED_EXCEPTION.
/// </summary>
public sealed class ShutdownOptions
{
    /// <summary>
    /// Сколько секунд ждать завершения текущих запросов при остановке,
    /// прежде чем прервать их принудительно.
    /// </summary>
    public int ShutdownTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Инициировать корректную остановку сервера при необработанном исключении.
    /// </summary>
    public bool ShutdownOnUnhandledException { get; set; } = true;
}
