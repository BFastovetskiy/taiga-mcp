using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TaigaMcp.Configuration;
using TaigaMcp.Health;
using TaigaMcp.Taiga;
using TaigaMcp.Tests.Support;
using Xunit;

namespace TaigaMcp.Tests;

public class ReadinessProbeTests
{
    private static (ReadinessProbe Probe, FakeHttpMessageHandler Handler, ServiceProvider Provider) Create(
        int cacheSeconds,
        Func<CapturedRequest, IReadOnlyList<CapturedRequest>, HttpResponseMessage>? responder = null)
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Token = "token",
            MaxRetries = 0,
        };
        var handler = new FakeHttpMessageHandler(responder
            ?? ((_, _) => FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"id":1}""")));

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton(new TaigaAuthStore(options));
        services.AddSingleton(new HealthOptions { ReadyCacheSeconds = cacheSeconds });
        services.AddHttpClient<TaigaClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddSingleton<ReadinessProbe>();

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<ReadinessProbe>(), handler, provider);
    }

    [Fact]
    public async Task CachedResult_IsReusedWithoutHittingApi()
    {
        var (probe, handler, provider) = Create(cacheSeconds: 60);
        using (provider)
        {
            var first = await probe.GetAsync();
            var second = await probe.GetAsync();
            var third = await probe.GetAsync();

            Assert.True(first.IsReady);
            Assert.True(second.IsReady);
            Assert.True(third.IsReady);
            // Реальный запрос к Taiga выполнен только один раз.
            Assert.Single(handler.Requests);
        }
    }

    [Fact]
    public async Task WithoutCache_ChecksApiEveryTime()
    {
        var (probe, handler, provider) = Create(cacheSeconds: 0);
        using (provider)
        {
            await probe.GetAsync();
            await probe.GetAsync();

            Assert.Equal(2, handler.Requests.Count);
        }
    }

    [Fact]
    public async Task NotReady_WhenApiUnavailable()
    {
        var (probe, _, provider) = Create(
            cacheSeconds: 60,
            (_, _) => FakeHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable));

        using (provider)
        {
            var result = await probe.GetAsync();

            Assert.False(result.IsReady);
            Assert.NotNull(result.Reason);
        }
    }
}
