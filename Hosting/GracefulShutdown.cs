using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaigaMcp.Configuration;

namespace TaigaMcp.Hosting;

/// <summary>
/// Корректное завершение работы сервера:
///  - логирование сигналов остановки (SIGTERM / Ctrl+C);
///  - инициирование остановки при необработанном исключении (если включено).
/// Таймаут ожидания задаётся через <see cref="HostOptions.ShutdownTimeout"/> в Program.
/// </summary>
public static class GracefulShutdown
{
    /// <summary>Логгер по умолчанию для событий завершения.</summary>
    public const string LoggerCategory = "TaigaMcp.Shutdown";

    /// <summary>Регистрирует обработчики событий завершения и критических ошибок.</summary>
    public static void Register(
        ILogger logger,
        IHostApplicationLifetime lifetime,
        ShutdownOptions options)
    {
        lifetime.ApplicationStopping.Register(
            () => logger.LogInformation("Получен сигнал остановки, завершаем работу..."));

        lifetime.ApplicationStopped.Register(
            () => logger.LogInformation("Сервер остановлен."));

        if (!options.ShutdownOnUnhandledException)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            HandleFatal(logger, lifetime, eventArgs.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            logger.LogError(
                eventArgs.Exception,
                "Необработанное исключение в задаче (не фатально).");
            eventArgs.SetObserved();
        };
    }

    /// <summary>
    /// Обрабатывает фатальное исключение: пишет критическую запись в лог и
    /// инициирует корректную остановку приложения.
    /// </summary>
    public static void HandleFatal(ILogger logger, IHostApplicationLifetime lifetime, Exception? exception)
    {
        logger.LogCritical(exception, "Необработанное исключение — инициируется остановка сервера.");
        lifetime.StopApplication();
    }
}
