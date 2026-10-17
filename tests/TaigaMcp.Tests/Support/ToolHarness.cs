using System.Text;
using Microsoft.AspNetCore.Http;
using Prometheus;
using TaigaMcp.Configuration;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tests.Support;

/// <summary>
/// Собирает инструмент MCP с поддельным HTTP-транспортом, аудит-логгером и метриками,
/// чтобы тестировать поведение инструментов без реального сервера Taiga.
/// </summary>
internal sealed class ToolHarness : IDisposable
{
    private readonly TaigaAuthStore _authStore;

    public ToolHarness(
        Func<CapturedRequest, IReadOnlyList<CapturedRequest>, HttpResponseMessage> responder,
        TaigaOptions? options = null,
        AuditOptions? auditOptions = null,
        HttpContext? httpContext = null)
    {
        Options = options ?? new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Token = "token",
            MaxRetries = 0,
        };

        Handler = new FakeHttpMessageHandler(responder);
        _authStore = new TaigaAuthStore(Options);
        Client = new TaigaClient(new HttpClient(Handler), Options, _authStore);

        Registry = new CollectorRegistry();
        Metrics = new McpMetrics(Registry);

        AuditOptions = auditOptions ?? new AuditOptions { Enabled = false, FilePath = null };
        var accessor = new HttpContextAccessor();
        if (httpContext is not null)
        {
            accessor.HttpContext = httpContext;
        }

        Audit = new AuditLogger(CapturingLogger, accessor, AuditOptions);
    }

    public TaigaOptions Options { get; }
    public FakeHttpMessageHandler Handler { get; }
    public TaigaClient Client { get; }
    public CollectorRegistry Registry { get; }
    public McpMetrics Metrics { get; }
    public AuditOptions AuditOptions { get; }
    public AuditLogger Audit { get; }
    public CapturingLogger<AuditLogger> CapturingLogger { get; } = new();

    public async Task<string> ExportMetricsAsync()
    {
        using var stream = new MemoryStream();
        await Registry.CollectAndExportAsTextAsync(stream, CancellationToken.None);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public void Dispose()
    {
        _authStore.Dispose();
    }
}
