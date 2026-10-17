using System.Net;
using TaigaMcp.Configuration;
using TaigaMcp.Health;
using TaigaMcp.Taiga;
using TaigaMcp.Tests.Support;
using Xunit;

namespace TaigaMcp.Tests;

public class HealthChecksTests
{
    private static TaigaClient CreateClient(
        TaigaOptions options,
        Func<CapturedRequest, IReadOnlyList<CapturedRequest>, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        return new TaigaClient(new HttpClient(handler), options, new TaigaAuthStore(options));
    }

    [Fact]
    public async Task Ready_WhenApiRespondsOk()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Token = "token",
            MaxRetries = 0,
        };
        var client = CreateClient(options, (_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"id":1,"username":"user"}"""));

        var result = await HealthChecks.CheckReadyAsync(client, options);

        Assert.True(result.IsReady);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task NotReady_WhenApiReturnsError()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Token = "token",
            MaxRetries = 0,
        };
        var client = CreateClient(options, (_, _) =>
            FakeHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable));

        var result = await HealthChecks.CheckReadyAsync(client, options);

        Assert.False(result.IsReady);
        Assert.NotNull(result.Reason);
    }
}
