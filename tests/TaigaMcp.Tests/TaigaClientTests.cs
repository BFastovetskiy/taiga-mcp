using System.Net;
using TaigaMcp.Configuration;
using TaigaMcp.Taiga;
using TaigaMcp.Tests.Support;
using Xunit;

namespace TaigaMcp.Tests;

public class TaigaClientTests
{
    private static (TaigaClient Client, FakeHttpMessageHandler Handler) CreateClient(
        TaigaOptions options,
        Func<CapturedRequest, IReadOnlyList<CapturedRequest>, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var store = new TaigaAuthStore(options);
        var client = new TaigaClient(new HttpClient(handler), options, store);
        return (client, handler);
    }

    private static int CountOf(IReadOnlyList<CapturedRequest> requests, HttpMethod method, string pathContains)
        => requests.Count(r => r.Matches(method, pathContains));

    [Fact]
    public async Task Login_PerformedOnce_AndTokenReused()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Username = "user",
            Password = "pass",
            MaxRetries = 0,
        };

        var (client, handler) = CreateClient(options, (request, _) =>
            request.Uri.Contains("/auth", StringComparison.OrdinalIgnoreCase)
                ? FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"auth_token":"tok","refresh":"r"}""")
                : FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));

        await client.GetAsync("projects");
        await client.GetAsync("projects");

        Assert.Equal(1, CountOf(handler.Requests, HttpMethod.Post, "/auth"));
        Assert.Equal(2, CountOf(handler.Requests, HttpMethod.Get, "/projects"));
    }

    [Fact]
    public async Task Unauthorized_TriggersSingleReauthentication()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Token = "expired",
            Username = "user",
            Password = "pass",
            MaxRetries = 0,
        };

        var (client, handler) = CreateClient(options, (request, all) =>
        {
            if (request.Uri.Contains("/auth", StringComparison.OrdinalIgnoreCase))
            {
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"auth_token":"fresh"}""");
            }

            return CountOf(all, HttpMethod.Get, "/projects") == 1
                ? FakeHttpMessageHandler.Json(HttpStatusCode.Unauthorized, """{"detail":"expired"}""")
                : FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
        });

        await client.GetAsync("projects");

        Assert.Equal(1, CountOf(handler.Requests, HttpMethod.Post, "/auth"));
        Assert.Equal(2, CountOf(handler.Requests, HttpMethod.Get, "/projects"));
    }

    [Fact]
    public async Task TransientStatus_IsRetried()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Token = "token",
            MaxRetries = 2,
            RetryBaseDelayMs = 0,
        };

        var (client, handler) = CreateClient(options, (_, all) =>
            CountOf(all, HttpMethod.Get, "/projects") <= 2
                ? FakeHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable)
                : FakeHttpMessageHandler.Json(HttpStatusCode.OK, """[{"id":1}]"""));

        await client.GetAsync("projects");

        Assert.Equal(3, CountOf(handler.Requests, HttpMethod.Get, "/projects"));
    }

    [Fact]
    public async Task SendsPaginationHeaderAndQueryParameters()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Token = "token",
            DisablePaginationByDefault = true,
            MaxRetries = 0,
        };

        var (client, handler) = CreateClient(options, (_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));

        await client.GetAsync("projects", new Dictionary<string, string?>
        {
            ["member"] = "5",
            ["empty"] = null,
            ["q"] = "a b",
        });

        var request = Assert.Single(handler.Requests);
        Assert.Equal("True", request.Header("x-disable-pagination"));
        Assert.Contains("member=5", request.Uri);
        // Пробел в значении кодируется как %20; Uri может отображать его и как пробел.
        Assert.True(
            request.Uri.Contains("q=a%20b", StringComparison.Ordinal) ||
            request.Uri.Contains("q=a b", StringComparison.Ordinal),
            $"Unexpected URI: {request.Uri}");
        Assert.DoesNotContain("empty=", request.Uri);
    }

    [Fact]
    public async Task CookieMode_SendsCookieAndNoAuthorizationHeader()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            AuthMethod = "cookie",
            Cookie = "sessionid=abc123",
            MaxRetries = 0,
        };

        var (client, handler) = CreateClient(options, (_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));

        await client.GetAsync("projects");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("sessionid=abc123", request.Header("Cookie"));
        Assert.Null(request.Header("Authorization"));
    }

    [Fact]
    public async Task TokenAuthMode_SendsBearerHeader()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            AuthMethod = "token",
            Token = "abc",
            MaxRetries = 0,
        };

        var (client, handler) = CreateClient(options, (_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));

        await client.GetAsync("projects");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer abc", request.Header("Authorization"));
    }

    [Fact]
    public async Task NonSuccessResponse_ThrowsTaigaApiException()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Token = "token",
            MaxRetries = 0,
        };

        var (client, _) = CreateClient(options, (_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.NotFound, """{"detail":"not found"}"""));

        var ex = await Assert.ThrowsAsync<TaigaApiException>(() => client.GetAsync("projects/999"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_IsUsedBeforeLogin()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            RefreshToken = "refresh-1",
            MaxRetries = 0,
        };

        var (client, handler) = CreateClient(options, (request, _) =>
            request.Uri.Contains("/auth/refresh", StringComparison.OrdinalIgnoreCase)
                ? FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"auth_token":"refreshed"}""")
                : FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));

        await client.GetAsync("projects");

        // Обновление токена выполнено, обычный вход по логину/паролю не потребовался.
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.Uri.Contains("/auth/refresh"));
        Assert.DoesNotContain(handler.Requests, r =>
            r.Method == HttpMethod.Post && r.Uri.EndsWith("/auth", StringComparison.Ordinal));

        var projectsRequest = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Get);
        Assert.Equal("Bearer refreshed", projectsRequest.Header("Authorization"));
    }

    [Fact]
    public async Task NetworkError_IsRetried_ForIdempotentRequest()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Token = "token",
            MaxRetries = 2,
            RetryBaseDelayMs = 0,
        };

        var (client, handler) = CreateClient(options, (_, all) =>
            CountOf(all, HttpMethod.Get, "/projects") == 1
                ? throw new HttpRequestException("network down")
                : FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));

        await client.GetAsync("projects");

        Assert.Equal(2, CountOf(handler.Requests, HttpMethod.Get, "/projects"));
    }

    [Fact]
    public async Task NetworkError_IsNotRetried_ForPost()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Token = "token",
            MaxRetries = 2,
            RetryBaseDelayMs = 0,
        };

        var (client, handler) = CreateClient(options, (_, _) =>
            throw new HttpRequestException("network down"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.PostAsync("projects", JsonBody.Object(("name", "x"))));

        Assert.Equal(1, CountOf(handler.Requests, HttpMethod.Post, "/projects"));
    }

    [Fact]
    public async Task LoginResponse_WithInvalidJson_ThrowsTaigaApiException()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Username = "user",
            Password = "pass",
            MaxRetries = 0,
        };

        var (client, _) = CreateClient(options, (request, _) =>
            request.Uri.Contains("/auth", StringComparison.OrdinalIgnoreCase)
                ? FakeHttpMessageHandler.Json(HttpStatusCode.OK, "это вовсе не JSON")
                : FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));

        await Assert.ThrowsAsync<TaigaApiException>(() => client.GetAsync("projects"));
    }

    [Fact]
    public async Task LoginResponse_WithoutAuthToken_ThrowsTaigaApiException()
    {
        var options = new TaigaOptions
        {
            BaseUrl = "https://taiga.test/api/v1",
            Username = "user",
            Password = "pass",
            MaxRetries = 0,
        };

        var (client, _) = CreateClient(options, (request, _) =>
            request.Uri.Contains("/auth", StringComparison.OrdinalIgnoreCase)
                ? FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"foo":"bar"}""")
                : FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));

        await Assert.ThrowsAsync<TaigaApiException>(() => client.GetAsync("projects"));
    }
}
