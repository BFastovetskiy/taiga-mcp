using TaigaMcp.Configuration;
using TaigaMcp.Taiga;
using Xunit;

namespace TaigaMcp.Tests;

public class TaigaAuthStoreTests
{
    [Fact]
    public void Constructor_CopiesConfiguredTokens()
    {
        var store = new TaigaAuthStore(new TaigaOptions
        {
            Token = "configured-token",
            RefreshToken = "configured-refresh",
        });

        Assert.Equal("configured-token", store.AuthToken);
        Assert.Equal("configured-refresh", store.RefreshToken);
    }

    [Fact]
    public void Constructor_WithoutToken_LeavesAuthTokenNull()
    {
        var store = new TaigaAuthStore(new TaigaOptions());

        Assert.Null(store.AuthToken);
        Assert.Null(store.RefreshToken);
    }

    [Fact]
    public void Tokens_CanBeReplaced()
    {
        var store = new TaigaAuthStore(new TaigaOptions());

        store.AuthToken = "a1";
        store.RefreshToken = "r1";
        store.AuthToken = "a2";

        Assert.Equal("a2", store.AuthToken);
        Assert.Equal("r1", store.RefreshToken);
    }

    [Fact]
    public void AuthToken_IsVisibleAcrossThreads()
    {
        var store = new TaigaAuthStore(new TaigaOptions());
        var values = Enumerable.Range(0, 8).Select(i => $"token-{i}").ToArray();
        var observed = new string?[values.Length];

        Parallel.For(0, 512, i =>
        {
            var value = values[i % values.Length];
            store.AuthToken = value;
            observed[i % values.Length] = store.AuthToken;
        });

        Assert.All(observed, value => Assert.NotNull(value));
        Assert.Contains(store.AuthToken, values);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var store = new TaigaAuthStore(new TaigaOptions());

        store.Dispose();
        store.Dispose();
    }
}
