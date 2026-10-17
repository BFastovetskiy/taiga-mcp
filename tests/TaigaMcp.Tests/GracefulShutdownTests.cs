using TaigaMcp.Configuration;
using TaigaMcp.Hosting;
using TaigaMcp.Tests.Support;
using Xunit;

namespace TaigaMcp.Tests;

public class GracefulShutdownTests
{
    [Fact]
    public void HandleFatal_LogsCriticalAndStopsApplication()
    {
        var logger = new CapturingLogger<object>();
        var lifetime = new FakeHostApplicationLifetime();

        GracefulShutdown.HandleFatal(logger, lifetime, new InvalidOperationException("boom"));

        Assert.True(lifetime.StopRequested);
        Assert.Contains(logger.Messages, message =>
            message.Contains("остановка", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HandleFatal_WithoutException_StillStopsApplication()
    {
        var logger = new CapturingLogger<object>();
        var lifetime = new FakeHostApplicationLifetime();

        GracefulShutdown.HandleFatal(logger, lifetime, null);

        Assert.True(lifetime.StopRequested);
    }

    [Fact]
    public void Register_LogsOnStoppingAndStopped()
    {
        var logger = new CapturingLogger<object>();
        using var cts = new CancellationTokenSource();
        var lifetime = new RecordingHostApplicationLifetime(cts.Token);

        // Отключаем подписку на глобальные события, чтобы не влиять на другие тесты.
        GracefulShutdown.Register(
            logger,
            lifetime,
            new ShutdownOptions { ShutdownOnUnhandledException = false });

        cts.Cancel();

        Assert.Contains(logger.Messages, message =>
            message.Contains("завершаем работу", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, message =>
            message.Contains("остановлен", StringComparison.OrdinalIgnoreCase));
    }
}
