using System.Net;

namespace TaigaMcp.Taiga;

/// <summary>
/// Исключение, возникающее, когда Taiga API вернул ответ с кодом ошибки (4xx/5xx).
/// </summary>
public sealed class TaigaApiException : Exception
{
    /// <summary>HTTP-статус ответа Taiga.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Тело ответа (обычно JSON с описанием ошибки).</summary>
    public string ResponseBody { get; }

    public TaigaApiException(HttpStatusCode statusCode, string responseBody)
        : base($"Taiga API вернул ошибку {(int)statusCode} ({statusCode}). {responseBody}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
