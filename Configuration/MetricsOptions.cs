namespace TaigaMcp.Configuration;

/// <summary>
/// Настройки экспорта метрик Prometheus.
/// Берутся из секции "Metrics" и могут быть переопределены переменными
/// METRICS_ENABLED / METRICS_PATH.
/// </summary>
public sealed class MetricsOptions
{
    /// <summary>Включён ли сбор и экспорт метрик.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Путь, по которому отдаются метрики Prometheus.</summary>
    public string Path { get; set; } = "/metrics";
}
