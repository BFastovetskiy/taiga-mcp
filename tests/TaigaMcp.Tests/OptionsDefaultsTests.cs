using TaigaMcp.Configuration;
using Xunit;

namespace TaigaMcp.Tests;

public class OptionsDefaultsTests
{
    [Fact]
    public void TaigaOptions_DefaultTimeout_IsThirtySeconds()
    {
        Assert.Equal(30, new TaigaOptions().TimeoutSeconds);
    }

    [Fact]
    public void RateLimitOptions_Defaults_AreEnabled()
    {
        var options = new RateLimitOptions();

        Assert.True(options.Enabled);
        Assert.True(options.PermitLimit > 0);
        Assert.True(options.WindowSeconds > 0);
    }

    [Fact]
    public void MetricsOptions_DefaultPath_IsMetrics()
    {
        Assert.Equal("/metrics", new MetricsOptions().Path);
    }

    [Fact]
    public void ShutdownOptions_Defaults()
    {
        var options = new ShutdownOptions();

        Assert.Equal(30, options.ShutdownTimeoutSeconds);
        Assert.True(options.ShutdownOnUnhandledException);
    }

    [Fact]
    public void ServerOptions_Defaults()
    {
        var options = new ServerOptions();

        Assert.Equal("0.0.0.0", options.Host);
        Assert.True(options.Port > 0);
    }

    [Fact]
    public void AuditOptions_Defaults_AreEnabledWithFilePath()
    {
        var options = new AuditOptions();

        Assert.True(options.Enabled);
        Assert.NotNull(options.FilePath);
        Assert.Equal(64, options.MaxFileSizeMB);
        Assert.True(options.MaxBackupFiles > 0);
    }

    [Fact]
    public void HealthOptions_DefaultReadyCache_IsPositive()
    {
        Assert.True(new HealthOptions().ReadyCacheSeconds > 0);
    }
}
