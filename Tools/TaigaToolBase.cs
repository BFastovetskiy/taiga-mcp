using System.Globalization;
using System.Runtime.CompilerServices;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>
/// Базовый класс инструментов MCP.
///
/// Хранит клиента Taiga, аудит-лог и метрики, а также предоставляет обёртки выполнения,
/// которые преобразуют исключения в понятное текстовое сообщение для клиента,
/// фиксируют метрики и (для деструктивных инструментов) пишут запись в аудит-лог.
/// </summary>
public abstract class TaigaToolBase
{
    protected TaigaToolBase(TaigaClient client, AuditLogger audit, McpMetrics metrics)
    {
        Client = client;
        Audit = audit;
        Metrics = metrics;
    }

    /// <summary>Клиент Taiga REST API.</summary>
    protected TaigaClient Client { get; }

    /// <summary>Аудит-лог деструктивных операций.</summary>
    protected AuditLogger Audit { get; }

    /// <summary>Метрики вызовов инструментов (Prometheus).</summary>
    protected McpMetrics Metrics { get; }

    /// <summary>
    /// Выполняет действие инструмента и преобразует исключения в понятное текстовое сообщение,
    /// чтобы клиент MCP получил описание ошибки, а не падение сервера.
    /// Имя инструмента для метрик определяется по имени вызывающего метода.
    /// </summary>
    protected async Task<string> RunAsync(Func<Task<string>> action, [CallerMemberName] string memberName = "")
    {
        var tool = ToolNameResolver.Resolve(GetType(), memberName);
        using var timer = Metrics.MeasureDuration(tool);
        try
        {
            var result = await action().ConfigureAwait(false);
            Metrics.Observe(tool, success: true);
            return result;
        }
        catch (Exception ex)
        {
            Metrics.Observe(tool, success: false);
            return FormatError(ex);
        }
    }

    /// <summary>
    /// Выполняет деструктивный инструмент: пишет запись в аудит-лог
    /// (имя инструмента, аргументы, результат, IP клиента, время) и фиксирует метрики.
    /// </summary>
    protected async Task<string> RunDestructiveAsync(
        string toolName,
        object? arguments,
        Func<Task<string>> action)
    {
        using var timer = Metrics.MeasureDuration(toolName);
        try
        {
            var result = await action().ConfigureAwait(false);
            Metrics.Observe(toolName, success: true);
            Audit.Record(toolName, arguments, success: true, error: null);
            return result;
        }
        catch (Exception ex)
        {
            Metrics.Observe(toolName, success: false);
            Audit.Record(toolName, arguments, success: false, error: ex.Message);
            return FormatError(ex);
        }
    }

    private static string FormatError(Exception ex)
    {
        if (ex is TaigaApiException api)
        {
            return $"Ошибка Taiga API ({(int)api.StatusCode} {api.StatusCode}):\n{api.ResponseBody}";
        }
        return "Ошибка выполнения инструмента: " + ex.Message;
    }

    protected static string? Q(int? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    protected static string? Q(bool? value)
        => value.HasValue ? (value.Value ? "true" : "false") : null;
}
