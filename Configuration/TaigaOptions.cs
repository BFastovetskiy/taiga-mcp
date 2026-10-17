namespace TaigaMcp.Configuration;

/// <summary>
/// Настройки подключения к Taiga REST API.
/// Значения берутся из секции конфигурации "Taiga" (appsettings.json),
/// а также могут быть переопределены переменными окружения (TAIGA_*).
/// </summary>
public sealed class TaigaOptions
{
    /// <summary>
    /// Базовый адрес API Taiga. По умолчанию облачный сервер Taiga.
    /// Пример: https://api.taiga.io/api/v1 или http://localhost:8000/api/v1
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.taiga.io/api/v1";

    /// <summary>Логин (username или email) для стандартной аутентификации.</summary>
    public string? Username { get; set; }

    /// <summary>Пароль для стандартной аутентификации.</summary>
    public string? Password { get; set; }

    /// <summary>
    /// Готовый токен доступа (auth_token). Если задан — вход по логину/паролю не выполняется.
    /// Приоритетнее, чем Username/Password.
    ///
    /// Это альтернативный вариант авторизации для случаев, когда прямой вход по API недоступен
    /// (например, из-за SSO): токен можно взять из уже открытой сессии Taiga
    /// (localStorage ключ «taiga.auth_token» или заголовок Authorization) и передать сюда.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>Refresh-токен (необязательно).</summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Способ авторизации:
    ///  - "auto" (по умолчанию): сначала заданный Token, затем RefreshToken, затем вход по Username/Password;
    ///  - "password": всегда выполнять вход по Username/Password (даже если задан Token);
    ///  - "token": использовать только заданный Token, вход по API не выполняется
    ///             (вариант для SSO: токен берётся из уже открытой сессии Taiga);
    ///  - "cookie": отправлять заголовок Cookie (сессия SSO/прокси), заголовок Authorization не используется.
    /// </summary>
    public string AuthMethod { get; set; } = "auto";

    /// <summary>
    /// Значение заголовка Cookie для режима AuthMethod="cookie".
    /// Используется, когда доступ к API возможен только с сессионной cookie SSO
    /// (например, Taiga за обратным прокси с SSO-аутентификацией).
    /// Пример: "sessionid=abc123; csrftoken=xyz".
    /// </summary>
    public string? Cookie { get; set; }

    /// <summary>
    /// Схема авторизации в заголовке Authorization.
    /// "Bearer" — стандартный пользовательский токен Taiga (по умолчанию).
    /// "Application" — токен внешнего приложения (Authorization: Application {token}).
    /// </summary>
    public string AuthScheme { get; set; } = "Bearer";

    /// <summary>
    /// Путь к сертификату (PEM/DER/PFX), которому следует доверять.
    /// Если это корневой ЦС — выполняется проверка цепочки; если листовой/самоподписанный
    /// сертификат — pinning по отпечатку. Предпочтительнее, чем IgnoreSslErrors.
    /// Относительный путь разрешается относительно каталога сборки.
    /// </summary>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Игнорировать ошибки проверки TLS-сертификата сервера Taiga.
    /// Нужно для корпоративных или самоподписанных сертификатов, которым не доверяет ОС.
    /// Включать только для доверенных внутренних инсталляций.
    /// </summary>
    public bool IgnoreSslErrors { get; set; }

    /// <summary>
    /// Отключать пагинацию (заголовок x-disable-pagination) для списочных запросов по умолчанию.
    /// true — возвращать все записи сразу.
    /// </summary>
    public bool DisablePaginationByDefault { get; set; } = true;

    /// <summary>Таймаут HTTP-запросов к Taiga в секундах. По умолчанию 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Максимальное число повторных попыток при временных сбоях (сеть, 429/5xx).
    /// 0 — повторные попытки отключены.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Базовая задержка перед повтором (мс); растёт экспоненциально.</summary>
    public int RetryBaseDelayMs { get; set; } = 500;
}
