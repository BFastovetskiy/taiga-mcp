using System.Net;
using System.Text;

namespace TaigaMcp.Tests.Support;

/// <summary>Зафиксированный HTTP-запрос для проверок в тестах.</summary>
internal sealed record CapturedRequest(
    HttpMethod Method,
    string Uri,
    string Body,
    IReadOnlyDictionary<string, string> Headers)
{
    public string? Header(string name)
        => Headers.TryGetValue(name, out var value) ? value : null;

    public bool Matches(HttpMethod method, string pathContains)
        => Method == method && Uri.Contains(pathContains, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Поддельный HTTP-обработчик, возвращающий заранее заданные ответы.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<CapturedRequest, IReadOnlyList<CapturedRequest>, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(
        Func<CapturedRequest, IReadOnlyList<CapturedRequest>, HttpResponseMessage> responder)
        => _responder = responder;

    public List<CapturedRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }
        }

        var captured = new CapturedRequest(request.Method, request.RequestUri!.ToString(), body, headers);
        Requests.Add(captured);

        return _responder(captured, Requests);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(HttpStatusCode status)
        => new(status);
}
