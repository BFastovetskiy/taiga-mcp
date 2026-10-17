using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TaigaMcp.Configuration;

namespace TaigaMcp.Taiga;

/// <summary>
/// Тонкая обёртка над Taiga REST API.
///
/// Реализует:
///  - стандартную токен-аутентификацию (заголовок Authorization: Bearer ...);
///  - автоматический вход по логину/паролю и обновление токена при 401;
///  - OCC (Optimistic Concurrency Control) — параметр version передаётся в инструментах;
///  - управление пагинацией (заголовок x-disable-pagination).
/// </summary>
public sealed class TaigaClient
{
    private readonly HttpClient _http;
    private readonly TaigaOptions _options;
    private readonly TaigaAuthStore _authStore;

    // Настройки сериализации JSON: не пишем null и не экранируем кириллицу.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public TaigaClient(HttpClient http, TaigaOptions options, TaigaAuthStore authStore)
    {
        _http = http;
        _options = options;
        _authStore = authStore;

        // Нормализуем базовый адрес: убираем пробелы и завершающий слэш, затем добавляем
        // ровно один слэш — это обязательно для корректных относительных путей.
        var baseUrl = (options.BaseUrl ?? string.Empty).Trim().TrimEnd('/') + "/";
        _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        _http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ---------------------------------------------------------------------
    // Аутентификация
    // ---------------------------------------------------------------------

    /// <summary>Режим авторизации по cookie-сессии SSO (заголовок Authorization не используется).</summary>
    private bool IsCookieMode()
        => string.Equals(_options.AuthMethod?.Trim(), "cookie", StringComparison.OrdinalIgnoreCase);

    /// <summary>Возвращает действующий токен, при необходимости выполняя вход/обновление.</summary>
    private async Task<string> GetTokenAsync(bool force, CancellationToken ct)
    {
                var method = string.IsNullOrWhiteSpace(_options.AuthMethod) ? "auto" : _options.AuthMethod.Trim().ToLowerInvariant();

        // Читаем токен один раз: свойство потокобезопасно, но повторное чтение
        // могло бы вернуть уже другое значение между проверкой и возвратом.
        var cached = _authStore.AuthToken;
        if (!force && cached is not null)
        {
            return cached;
        }

        await _authStore.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = _authStore.AuthToken;
            if (!force && current is not null)
            {
                return current;
            }

            // Вариант для SSO: используется только заранее переданный токен.
            if (method == "token")
            {
                if (!string.IsNullOrWhiteSpace(_authStore.AuthToken))
                {
                    return _authStore.AuthToken!;
                }

                throw new InvalidOperationException(
                    "Режим AuthMethod=token требует заданного Token (auth_token из текущей сессии Taiga).");
            }

            // Режим "password": всегда выполняем вход по логину/паролю.
            if (method == "password")
            {
                await LoginAsync(ct).ConfigureAwait(false);
                return _authStore.AuthToken!;
            }

            // Автоматический режим: refresh → логин/пароль.
            // 1) Пробуем обновить токен по refresh-токену.
            if (!string.IsNullOrWhiteSpace(_authStore.RefreshToken))
            {
                try
                {
                    await RefreshTokenAsync(ct).ConfigureAwait(false);
                    return _authStore.AuthToken!;
                }
                catch (TaigaApiException)
                {
                    // Обновление не удалось — перейдём к обычному входу.
                }
            }

            // 2) Обычный вход по логину/паролю.
            if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                await LoginAsync(ct).ConfigureAwait(false);
                return _authStore.AuthToken!;
            }

            throw new InvalidOperationException(
                "Не заданы учётные данные Taiga. Укажите Token либо Username/Password в конфигурации.");
        }
        finally
        {
            _authStore.Gate.Release();
        }
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["type"] = "normal",
            ["username"] = _options.Username,
            ["password"] = _options.Password,
        };

        var (status, text, _) = await RawAsync(HttpMethod.Post, "auth", body, null, false, null, ct).ConfigureAwait(false);
        if (!IsSuccess(status))
        {
            throw new TaigaApiException(status, text);
        }

        var node = ParseAuthResponse(status, text, "аутентификация");
        _authStore.AuthToken = ReadAuthToken(node, status, "аутентификация");

        if (node["refresh"] is JsonValue refreshValue &&
            refreshValue.TryGetValue<string>(out var refreshToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            _authStore.RefreshToken = refreshToken;
        }
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        var body = new JsonObject { ["refresh"] = _authStore.RefreshToken };
        var (status, text, _) = await RawAsync(HttpMethod.Post, "auth/refresh", body, null, false, null, ct).ConfigureAwait(false);
        if (!IsSuccess(status))
        {
            throw new TaigaApiException(status, text);
        }

        var node = ParseAuthResponse(status, text, "обновление токена");
        _authStore.AuthToken = ReadAuthToken(node, status, "обновление токена");
    }

    /// <summary>
    /// Разбирает JSON-ответ аутентификации. Некорректный JSON не должен приводить
    /// к необработанному исключению — преобразуем его в <see cref="TaigaApiException"/>.
    /// </summary>
    private static JsonObject ParseAuthResponse(HttpStatusCode status, string text, string stage)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject
                ?? throw new TaigaApiException(status, $"Ответ ({stage}) не является JSON-объектом: {text}");
        }
        catch (JsonException ex)
        {
            throw new TaigaApiException(status, $"Некорректный JSON в ответе ({stage}): {ex.Message}. Ответ: {text}");
        }
    }

    /// <summary>Извлекает строковое поле auth_token, не падая на неверном типе значения.</summary>
    private static string ReadAuthToken(JsonObject node, HttpStatusCode status, string stage)
    {
        if (node["auth_token"] is JsonValue value &&
            value.TryGetValue<string>(out var token) &&
            !string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        throw new TaigaApiException(status, $"В ответе ({stage}) отсутствует корректное поле auth_token.");
    }

    // ---------------------------------------------------------------------
    // Публичные операции
    // ---------------------------------------------------------------------

    /// <summary>GET-запрос.</summary>
    public Task<string> GetAsync(string path, IDictionary<string, string?>? query = null, bool? disablePagination = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Get, path, null, query, disablePagination, ct);

    /// <summary>POST-запрос с телом.</summary>
    public Task<string> PostAsync(string path, JsonNode? body = null, IDictionary<string, string?>? query = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, path, body, query, null, ct);

    /// <summary>PUT-запрос (полная замена объекта).</summary>
    public Task<string> PutAsync(string path, JsonNode? body = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Put, path, body, null, null, ct);

    /// <summary>PATCH-запрос (частичное изменение).</summary>
    public Task<string> PatchAsync(string path, JsonNode? body = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Patch, path, body, null, null, ct);

    /// <summary>DELETE-запрос.</summary>
    public Task<string> DeleteAsync(string path, IDictionary<string, string?>? query = null, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, path, null, query, null, ct);

    /// <summary>
    /// Основной метод выполнения запроса. Автоматически подставляет токен,
    /// а при ответе 401 один раз повторяет запрос после переаутентификации.
    /// </summary>
    public async Task<string> SendAsync(
        HttpMethod method,
        string path,
        JsonNode? body = null,
        IDictionary<string, string?>? query = null,
        bool? disablePagination = null,
        CancellationToken ct = default)
    {
        var disable = disablePagination ?? _options.DisablePaginationByDefault;

        // В режиме cookie-авторизации токен не требуется.
        string? token = IsCookieMode() ? null : await GetTokenAsync(false, ct).ConfigureAwait(false);

        var (status, text, headers) = await SendWithRetryAsync(method, path, body, query, disable, token, ct).ConfigureAwait(false);

        if (status == HttpStatusCode.Unauthorized && !IsCookieMode())
        {
            // Токен протух — обновляем и повторяем один раз.
            token = await GetTokenAsync(true, ct).ConfigureAwait(false);
            (status, text, headers) = await SendWithRetryAsync(method, path, body, query, disable, token, ct).ConfigureAwait(false);
        }

        if (!IsSuccess(status))
        {
            throw new TaigaApiException(status, string.IsNullOrWhiteSpace(text) ? "(пустой ответ)" : text);
        }

        return FormatResponse(status, text, headers);
    }

    /// <summary>
    /// Выполняет запрос с повторными попытками при временных сбоях.
    /// Повторяем при статусах 408/429/5xx и при сетевых исключениях (для идемпотентных
    /// методов), с экспоненциальной задержкой и учётом заголовка Retry-After.
    /// </summary>
    private async Task<(HttpStatusCode Status, string Body, HttpResponseHeaders Headers)> SendWithRetryAsync(
        HttpMethod method,
        string path,
        JsonNode? body,
        IDictionary<string, string?>? query,
        bool disablePagination,
        string? token,
        CancellationToken ct)
    {
        var maxRetries = Math.Max(0, _options.MaxRetries);
        var attempt = 0;

        while (true)
        {
            try
            {
                var result = await RawAsync(method, path, body, query, disablePagination, token, ct).ConfigureAwait(false);

                if (attempt < maxRetries && IsTransientStatus(result.Status))
                {
                    await DelayBeforeRetryAsync(attempt, result.Headers, ct).ConfigureAwait(false);
                    attempt++;
                    continue;
                }

                return result;
            }
            catch (Exception ex) when (attempt < maxRetries && IsIdempotent(method) && IsTransientException(ex, ct))
            {
                await DelayBeforeRetryAsync(attempt, null, ct).ConfigureAwait(false);
                attempt++;
            }
        }
    }

    private static bool IsTransientStatus(HttpStatusCode status)
        => status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static bool IsIdempotent(HttpMethod method)
        => method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Put
           || method == HttpMethod.Delete || method == HttpMethod.Options;

    private static bool IsTransientException(Exception ex, CancellationToken ct)
        => ex is HttpRequestException or IOException
           || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    private async Task DelayBeforeRetryAsync(int attempt, HttpResponseHeaders? headers, CancellationToken ct)
    {
        // Учитываем Retry-After, если сервер его прислал.
        var retryAfter = headers?.RetryAfter?.Delta
            ?? (headers?.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        if (retryAfter is { } ra && ra > TimeSpan.Zero && ra <= TimeSpan.FromSeconds(60))
        {
            await Task.Delay(ra, ct).ConfigureAwait(false);
            return;
        }

        var baseMs = Math.Max(0, _options.RetryBaseDelayMs);
        var delayMs = Math.Min(baseMs * Math.Pow(2, attempt), 10_000);
        if (delayMs > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct).ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------------
    // Низкоуровневый HTTP
    // ---------------------------------------------------------------------

    private async Task<(HttpStatusCode Status, string Body, HttpResponseHeaders Headers)> RawAsync(
        HttpMethod method,
        string path,
        JsonNode? body,
        IDictionary<string, string?>? query,
        bool disablePagination,
        string? token,
        CancellationToken ct)
    {
        var url = AppendQuery(path, query);

        using var request = new HttpRequestMessage(method, url);

        if (!string.IsNullOrWhiteSpace(_options.Cookie))
        {
            // Сессионная авторизация SSO: передаём cookie как есть.
            request.Headers.TryAddWithoutValidation("Cookie", _options.Cookie);
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            // Схема авторизации настраивается: "Bearer" (пользовательский токен)
            // или "Application" (токен внешнего приложения Taiga).
            var scheme = string.IsNullOrWhiteSpace(_options.AuthScheme) ? "Bearer" : _options.AuthScheme.Trim();
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);
        }

        if (disablePagination)
        {
            request.Headers.TryAddWithoutValidation("x-disable-pagination", "True");
        }

        if (body is not null)
        {
            var json = body.ToJsonString(WriteOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return (response.StatusCode, text, response.Headers);
    }

    private static bool IsSuccess(HttpStatusCode status) => (int)status is >= 200 and <= 299;

    private static string AppendQuery(string path, IDictionary<string, string?>? query)
    {
        if (query is null || query.Count == 0)
        {
            return path;
        }

        var parts = new List<string>();
        foreach (var kv in query)
        {
            if (string.IsNullOrEmpty(kv.Value))
            {
                continue;
            }

            parts.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}");
        }

        if (parts.Count == 0)
        {
            return path;
        }

        var separator = path.Contains('?') ? "&" : "?";
        return path + separator + string.Join('&', parts);
    }

    private static string FormatResponse(HttpStatusCode status, string text, HttpResponseHeaders headers)
    {
        // Пустой ответ (например, 204 No Content) — вернём служебный JSON.
        if (string.IsNullOrWhiteSpace(text))
        {
            return $"{{\"status\": {(int)status}, \"message\": \"{status}\"}}";
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            // Ответ не является JSON — отдаём как есть.
            return text;
        }

        if (node is null)
        {
            return $"\"{(int)status} {status}\"";
        }

        return node.ToJsonString(PrettyOptions);
    }
}
