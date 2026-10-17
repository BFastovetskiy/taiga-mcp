using System.Globalization;
using System.Text;
using Prometheus;
using TaigaMcp.Metrics;
using TaigaMcp.Tools;
using Xunit;

namespace TaigaMcp.Tests;

public class MetricsTests
{
    private static async Task<string> ExportAsync(CollectorRegistry registry)
    {
        using var stream = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(stream, CancellationToken.None);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // Суммирует значения всех образцов указанной метрики (без учёта меток).
    private static double SumMetric(string exposition, string metricName)
    {
        double total = 0;
        foreach (var rawLine in exposition.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var nameEnd = line.IndexOfAny(new[] { '{', ' ' });
            if (nameEnd <= 0 || line[..nameEnd] != metricName)
            {
                continue;
            }

            var separator = line.LastIndexOf(' ');
            if (separator > 0 &&
                double.TryParse(line[(separator + 1)..], NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                total += value;
            }
        }

        return total;
    }

    [Fact]
    public async Task Observe_TracksSuccessAndError()
    {
        var registry = new CollectorRegistry();
        var metrics = new McpMetrics(registry);

        metrics.Observe("taiga_list_projects", success: true);
        metrics.Observe("taiga_list_projects", success: true);
        metrics.Observe("taiga_list_projects", success: false);

        var exposition = await ExportAsync(registry);

        Assert.Equal(3, SumMetric(exposition, McpMetrics.RequestsMetricName));
        Assert.Equal(1, SumMetric(exposition, McpMetrics.ErrorsMetricName));
        Assert.Contains("status=\"success\"", exposition, StringComparison.Ordinal);
        Assert.Contains("status=\"error\"", exposition, StringComparison.Ordinal);
        Assert.Contains("tool=\"taiga_list_projects\"", exposition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MeasureDuration_RecordsHistogramSamples()
    {
        var registry = new CollectorRegistry();
        var metrics = new McpMetrics(registry);

        using (metrics.MeasureDuration("taiga_get_project"))
        {
        }

        using (metrics.MeasureDuration("taiga_get_project"))
        {
        }

        var exposition = await ExportAsync(registry);

        Assert.Equal(2, SumMetric(exposition, McpMetrics.DurationMetricName + "_count"));
    }

    [Fact]
    public async Task Observe_WithEmptyTool_UsesUnknownLabel()
    {
        var registry = new CollectorRegistry();
        var metrics = new McpMetrics(registry);

        metrics.Observe("", success: true);

        var exposition = await ExportAsync(registry);

        Assert.Contains("tool=\"unknown\"", exposition, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveToolName_ReturnsMcpToolName()
    {
        var name = ToolNameResolver.Resolve(typeof(ProjectTools), nameof(ProjectTools.ListProjectsAsync));

        Assert.Equal("taiga_list_projects", name);
    }

    [Fact]
    public void ResolveToolName_ForUnknownMethod_ReturnsMethodName()
    {
        var name = ToolNameResolver.Resolve(typeof(ProjectTools), "NotAToolMethod");

        Assert.Equal("NotAToolMethod", name);
    }
}
