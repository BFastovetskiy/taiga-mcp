using Prometheus;

namespace TaigaMcp.Metrics;

/// <summary>
/// Метрики вызовов MCP-инструментов в формате Prometheus:
///  - taiga_mcp_tool_requests_total{tool,status}          — RPS (число вызовов);
///  - taiga_mcp_tool_errors_total{tool}                   — error rate (число ошибок);
///  - taiga_mcp_tool_request_duration_seconds{tool}       — latency (гистограмма длительности).
/// </summary>
public sealed class McpMetrics
{
    public const string RequestsMetricName = "taiga_mcp_tool_requests_total";
    public const string ErrorsMetricName = "taiga_mcp_tool_errors_total";
    public const string DurationMetricName = "taiga_mcp_tool_request_duration_seconds";

    public const string SuccessStatus = "success";
    public const string ErrorStatus = "error";

    private readonly Counter _requests;
    private readonly Counter _errors;
    private readonly Histogram _duration;

    /// <summary>Создаёт метрики в глобальном реестре Prometheus.</summary>
    public McpMetrics()
        : this(Prometheus.Metrics.DefaultRegistry)
    {
    }

    /// <summary>Создаёт метрики в указанном реестре (используется в тестах).</summary>
    public McpMetrics(CollectorRegistry registry)
    {
        var factory = Prometheus.Metrics.WithCustomRegistry(registry);

        _requests = factory.CreateCounter(
            RequestsMetricName,
            "Общее число вызовов MCP-инструментов.",
            new[] { "tool", "status" });

        _errors = factory.CreateCounter(
            ErrorsMetricName,
            "Число вызовов MCP-инструментов, завершившихся ошибкой.",
            new[] { "tool" });

        _duration = factory.CreateHistogram(
            DurationMetricName,
            "Длительность вызова MCP-инструмента в секундах.",
            new[] { "tool" });
    }

    /// <summary>Начинает измерение длительности вызова инструмента.</summary>
    public IDisposable MeasureDuration(string tool)
        => _duration.WithLabels(Normalize(tool)).NewTimer();

    /// <summary>Фиксирует результат вызова инструмента (успех/ошибка).</summary>
    public void Observe(string tool, bool success)
    {
        var name = Normalize(tool);
        _requests.WithLabels(name, success ? SuccessStatus : ErrorStatus).Inc();
        if (!success)
        {
            _errors.WithLabels(name).Inc();
        }
    }

    private static string Normalize(string? tool)
        => string.IsNullOrWhiteSpace(tool) ? "unknown" : tool;
}
