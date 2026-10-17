using System.Net;
using Microsoft.AspNetCore.Http;
using TaigaMcp.Configuration;
using TaigaMcp.Security;
using Xunit;

namespace TaigaMcp.Tests;

public class RateLimitSetupTests
{
    private static DefaultHttpContext CreateContext(string? remoteIp = null)
    {
        var context = new DefaultHttpContext();
        if (remoteIp is not null)
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        }

        return context;
    }

    [Fact]
    public void PartitionKey_PrefersApiKeyHeader()
    {
        var context = CreateContext("10.0.0.1");
        context.Request.Headers["X-Api-Key"] = "super-secret";

        var key = RateLimitSetup.ResolvePartitionKey(context);

        Assert.StartsWith("token:", key, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", key, StringComparison.Ordinal);
    }

    [Fact]
    public void PartitionKey_UsesBearerToken()
    {
        var context = CreateContext("10.0.0.2");
        context.Request.Headers.Authorization = "Bearer bearer-token";

        var key = RateLimitSetup.ResolvePartitionKey(context);

        Assert.StartsWith("token:", key, StringComparison.Ordinal);
    }

    [Fact]
    public void PartitionKey_FallsBackToIpAddress()
    {
        var context = CreateContext("192.168.1.5");

        var key = RateLimitSetup.ResolvePartitionKey(context);

        Assert.Equal("ip:192.168.1.5", key);
    }

    [Fact]
    public void PartitionKey_IsStableForSameToken()
    {
        var first = CreateContext("10.0.0.1");
        first.Request.Headers["X-Api-Key"] = "same-token";
        var second = CreateContext("10.0.0.9");
        second.Request.Headers["X-Api-Key"] = "same-token";

        Assert.Equal(
            RateLimitSetup.ResolvePartitionKey(first),
            RateLimitSetup.ResolvePartitionKey(second));
    }

    [Fact]
    public void PartitionKey_DiffersForDifferentTokens()
    {
        var first = CreateContext();
        first.Request.Headers["X-Api-Key"] = "token-a";
        var second = CreateContext();
        second.Request.Headers["X-Api-Key"] = "token-b";

        Assert.NotEqual(
            RateLimitSetup.ResolvePartitionKey(first),
            RateLimitSetup.ResolvePartitionKey(second));
    }

    [Fact]
    public void CreatePartition_UsesResolvedKey()
    {
        var context = CreateContext("10.0.0.7");
        var options = new RateLimitOptions { PermitLimit = 10, WindowSeconds = 30 };

        var partition = RateLimitSetup.CreatePartition(context, options);

        Assert.Equal("ip:10.0.0.7", partition.PartitionKey);
    }

    [Fact]
    public void CreatePartition_EnforcesPermitLimit()
    {
        var context = CreateContext("10.0.0.8");
        var options = new RateLimitOptions { PermitLimit = 2, WindowSeconds = 60, QueueLimit = 0 };

        var partition = RateLimitSetup.CreatePartition(context, options);
        using var limiter = partition.Factory(partition.PartitionKey);

        Assert.True(limiter.AttemptAcquire(1).IsAcquired);
        Assert.True(limiter.AttemptAcquire(1).IsAcquired);
        Assert.False(limiter.AttemptAcquire(1).IsAcquired);
    }
}
