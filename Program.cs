using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ModelContextProtocol.Protocol;
using Prometheus;
using TaigaMcp.Configuration;
using TaigaMcp.Health;
using TaigaMcp.Hosting;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Security;
using TaigaMcp.Taiga;

// ---------------------------------------------------------------------------
// Точка входа MCP-сервера для работы с Taiga.
//
// MCP доступен по HTTP через Streamable HTTP транспорт (ModelContextProtocol.AspNetCore).
// Точка подключения — корень приложения ("/").
//
// Настройки Taiga берутся из секции "Taiga" (appsettings.json), необязательного
// appsettings.Local.json (для локальных секретов) и переменных окружения:
//   TAIGA_URL, TAIGA_USERNAME, TAIGA_PASSWORD, TAIGA_TOKEN, TAIGA_REFRESH_TOKEN,
//   TAIGA_AUTH_METHOD, TAIGA_AUTH_SCHEME, TAIGA_COOKIE, TAIGA_CA_CERT_PATH,
//   TAIGA_IGNORE_SSL_ERRORS, TAIGA_TIMEOUT_SECONDS, TAIGA_MAX_RETRIES, TAIGA_RETRY_DELAY_MS.
// Адрес и порт прослушивания: секция "Server" или MCP_HOST / MCP_PORT.
// Аудит деструктивных инструментов: секция "Audit" или AUDIT_ENABLED / AUDIT_FILE_PATH,
//   AUDIT_MAX_FILE_SIZE_MB (по умолчанию 64) / AUDIT_MAX_BACKUP_FILES.
// Защита MCP-эндпоинта: Mcp:AuthToken или переменная окружения MCP_AUTH_TOKEN.
// Health-эндпоинты: /health, /health/live, /health/ready;
//   кэш readiness — секция "Health" или HEALTH_READY_CACHE_SECONDS.
// Метрики Prometheus: секция "Metrics" или METRICS_ENABLED / METRICS_PATH.
// Ограничение частоты запросов: секция "RateLimit" или RATE_LIMIT_*.
// Корректное завершение: секция "Shutdown" или SHUTDOWN_*.
// ---------------------------------------------------------------------------

// Настройки всегда ищем рядом со сборкой, а не в текущем каталоге,
// чтобы MCP-сервер корректно работал при запуске из любого рабочего каталога.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Локальный файл с секретами (не попадает в репозиторий), имеет приоритет над appsettings.json.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

// 0. Настройки веб-сервера: адрес и порт прослушивания (секция "Server").
//    По умолчанию слушаем 0.0.0.0 (все интерфейсы); можно ограничить до 127.0.0.1.
var serverOptions = new ServerOptions();
builder.Configuration.GetSection("Server").Bind(serverOptions);
serverOptions.Host = ReadEnv("MCP_HOST") ?? serverOptions.Host;
if (int.TryParse(ReadEnv("MCP_PORT"), out var configuredPort))
{
    serverOptions.Port = configuredPort;
}

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(ResolveHost(serverOptions.Host), serverOptions.Port));

// 1. Настройки подключения к Taiga.
var options = new TaigaOptions();
builder.Configuration.GetSection("Taiga").Bind(options);

// Переопределение значениями переменных окружения (если заданы).
options.BaseUrl = ReadEnv("TAIGA_URL") ?? options.BaseUrl;
options.Username = ReadEnv("TAIGA_USERNAME") ?? options.Username;
options.Password = ReadEnv("TAIGA_PASSWORD") ?? options.Password;
options.Token = ReadEnv("TAIGA_TOKEN") ?? options.Token;
options.RefreshToken = ReadEnv("TAIGA_REFRESH_TOKEN") ?? options.RefreshToken;
options.AuthMethod = ReadEnv("TAIGA_AUTH_METHOD") ?? options.AuthMethod;
options.AuthScheme = ReadEnv("TAIGA_AUTH_SCHEME") ?? options.AuthScheme;
if (int.TryParse(ReadEnv("TAIGA_TIMEOUT_SECONDS"), out var timeoutSeconds))
{
    options.TimeoutSeconds = timeoutSeconds;
}
options.Cookie = ReadEnv("TAIGA_COOKIE") ?? options.Cookie;
options.CaCertificatePath = ReadEnv("TAIGA_CA_CERT_PATH") ?? options.CaCertificatePath;
if (bool.TryParse(ReadEnv("TAIGA_IGNORE_SSL_ERRORS"), out var ignoreSsl))
{
    options.IgnoreSslErrors = ignoreSsl;
}
if (int.TryParse(ReadEnv("TAIGA_MAX_RETRIES"), out var maxRetries))
{
    options.MaxRetries = maxRetries;
}
if (int.TryParse(ReadEnv("TAIGA_RETRY_DELAY_MS"), out var retryDelay))
{
    options.RetryBaseDelayMs = retryDelay;
}

// Ранняя валидация корпоративного сертификата: ошибка конфигурации
// (отсутствует/повреждён/просрочен) выявляется сразу при старте, а не при первом запросе.
TaigaHttpHandlerFactory.ValidateConfiguration(options);

builder.Services.AddSingleton(options);

// Кэш токенов Taiga должен быть singleton, иначе вход выполняется на каждый вызов инструмента.
builder.Services.AddSingleton<TaigaAuthStore>();

// Аудит деструктивных инструментов: настройки + singleton-логгер.
var auditOptions = new AuditOptions();
builder.Configuration.GetSection("Audit").Bind(auditOptions);
if (bool.TryParse(ReadEnv("AUDIT_ENABLED"), out var auditEnabled))
{
    auditOptions.Enabled = auditEnabled;
}
auditOptions.FilePath = ReadEnv("AUDIT_FILE_PATH") ?? auditOptions.FilePath;
if (int.TryParse(ReadEnv("AUDIT_MAX_FILE_SIZE_MB"), out var auditMaxSize))
{
    auditOptions.MaxFileSizeMB = auditMaxSize;
}
if (int.TryParse(ReadEnv("AUDIT_MAX_BACKUP_FILES"), out var auditMaxBackups))
{
    auditOptions.MaxBackupFiles = auditMaxBackups;
}

builder.Services.AddSingleton(auditOptions);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditLogger>();

// Проверки состояния: кэширование readiness, чтобы не опрашивать Taiga на каждый запрос.
var healthOptions = new HealthOptions();
builder.Configuration.GetSection("Health").Bind(healthOptions);
if (int.TryParse(ReadEnv("HEALTH_READY_CACHE_SECONDS"), out var healthReadyCache))
{
    healthOptions.ReadyCacheSeconds = healthReadyCache;
}

builder.Services.AddSingleton(healthOptions);
builder.Services.AddSingleton<ReadinessProbe>();

// Метрики Prometheus (RPS, latency, error rate вызовов инструментов).
var metricsOptions = new MetricsOptions();
builder.Configuration.GetSection("Metrics").Bind(metricsOptions);
if (bool.TryParse(ReadEnv("METRICS_ENABLED"), out var metricsEnabled))
{
    metricsOptions.Enabled = metricsEnabled;
}
metricsOptions.Path = ReadEnv("METRICS_PATH") ?? metricsOptions.Path;
if (!metricsOptions.Path.StartsWith('/'))
{
    metricsOptions.Path = "/" + metricsOptions.Path;
}

builder.Services.AddSingleton<McpMetrics>();

// Ограничение частоты запросов к MCP-эндпоинту (partition по токену или IP).
var rateLimitOptions = new RateLimitOptions();
builder.Configuration.GetSection("RateLimit").Bind(rateLimitOptions);
if (bool.TryParse(ReadEnv("RATE_LIMIT_ENABLED"), out var rateLimitEnabled))
{
    rateLimitOptions.Enabled = rateLimitEnabled;
}
if (int.TryParse(ReadEnv("RATE_LIMIT_PERMIT"), out var rateLimitPermit))
{
    rateLimitOptions.PermitLimit = rateLimitPermit;
}
if (int.TryParse(ReadEnv("RATE_LIMIT_WINDOW_SECONDS"), out var rateLimitWindow))
{
    rateLimitOptions.WindowSeconds = rateLimitWindow;
}

if (rateLimitOptions.Enabled)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Глобальный лимитер: защищаем MCP-эндпоинт, но пропускаем health/metrics.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/health") ||
                (metricsOptions.Enabled && path.StartsWithSegments(metricsOptions.Path)))
            {
                return RateLimitPartition.GetNoLimiter("unprotected");
            }

            return RateLimitSetup.CreatePartition(context, rateLimitOptions);
        });

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.Headers.RetryAfter =
                rateLimitOptions.WindowSeconds.ToString(CultureInfo.InvariantCulture);
            if (!context.HttpContext.Response.HasStarted)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync("Too Many Requests", token);
            }
        };
    });
}

// Корректное завершение работы и реакция на критические ошибки.
var shutdownOptions = new ShutdownOptions();
builder.Configuration.GetSection("Shutdown").Bind(shutdownOptions);
if (int.TryParse(ReadEnv("SHUTDOWN_TIMEOUT_SECONDS"), out var shutdownTimeout))
{
    shutdownOptions.ShutdownTimeoutSeconds = shutdownTimeout;
}
if (bool.TryParse(ReadEnv("SHUTDOWN_ON_UNHANDLED_EXCEPTION"), out var shutdownOnUnhandled))
{
    shutdownOptions.ShutdownOnUnhandledException = shutdownOnUnhandled;
}
builder.Services.Configure<HostOptions>(
    host => host.ShutdownTimeout = TimeSpan.FromSeconds(Math.Max(1, shutdownOptions.ShutdownTimeoutSeconds)));

// 2. Типизированный HttpClient для Taiga API.
//    Корпоративный/самоподписанный сертификат обрабатывается в TaigaHttpHandlerFactory
//    (настройки проверены заранее при старте). Управление жизненным циклом обработчика
//    берёт на себя IHttpClientFactory: ручное освобождение HttpClient не требуется.
builder.Services
    .AddHttpClient<TaigaClient>()
    .ConfigurePrimaryHttpMessageHandler(() => TaigaHttpHandlerFactory.Create(options))
    .SetHandlerLifetime(TimeSpan.FromMinutes(2));

// 3. MCP-сервер: HTTP-транспорт + все инструменты из текущей сборки.
builder.Services
    .AddMcpServer(mcp =>
    {
        mcp.ServerInfo = new Implementation
        {
            Name = "taiga-mcp",
            Title = "Taiga MCP Server",
            Version = "1.0.0",
        };
        mcp.ServerInstructions =
            "Сервер предоставляет инструменты управления Taiga: проекты, пользовательские истории, " +
            "задачи, проблемы, эпики, роли, пользователи, участники/приглашения. " +
            "Для изменения объектов учитывайте параметр version (OCC — оптимистичный контроль версий).";
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// 3.1. Корректное завершение работы (таймаут задан через HostOptions.ShutdownTimeout).
{
    var shutdownLogger = app.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger(GracefulShutdown.LoggerCategory);
    var applicationLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    GracefulShutdown.Register(shutdownLogger, applicationLifetime, shutdownOptions);
}

// 3.2. HTTP-метрики Prometheus и rate limiting.
if (metricsOptions.Enabled)
{
    app.UseHttpMetrics();
    // Создаём метрики заранее, чтобы серии были видны в /metrics с момента старта.
    app.Services.GetRequiredService<McpMetrics>();
}

if (rateLimitOptions.Enabled)
{
    app.UseRateLimiter();
}

// 4. Защита MCP-эндпоинта общим токеном (заголовок X-Api-Key или Authorization: Bearer).
//    Health-check остаётся открытым. Если токен не задан — доступ не ограничивается (только для локальной работы).
var mcpAuthToken = builder.Configuration["Mcp:AuthToken"];
if (string.IsNullOrWhiteSpace(mcpAuthToken))
{
    mcpAuthToken = ReadEnv("MCP_AUTH_TOKEN");
}

if (string.IsNullOrWhiteSpace(mcpAuthToken))
{
    app.Logger.LogWarning(
        "MCP-эндпоинт не защищён: Mcp:AuthToken / MCP_AUTH_TOKEN не задан. " +
        "Сервер слушает {Host}:{Port}: при привязке к 0.0.0.0 эндпоинт будет доступен из сети " +
        "без аутентификации. Настоятельно рекомендуется задать токен.",
        serverOptions.Host,
        serverOptions.Port);
}
else
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") ||
            (metricsOptions.Enabled && path.StartsWithSegments(metricsOptions.Path)))
        {
            await next();
            return;
        }

        if (!IsAuthorized(context.Request, mcpAuthToken))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await next();
    });
}

// Health-check эндпоинты (не являются частью MCP): /health, /health/live, /health/ready.
app.MapHealthEndpoints();

// Метрики Prometheus (RPS, latency, error rate).
if (metricsOptions.Enabled)
{
    app.MapMetrics(metricsOptions.Path);
}

// Основной MCP-эндпоинт (Streamable HTTP).
app.MapMcp();

app.Run();

// Вспомогательный метод чтения переменной окружения.
static string? ReadEnv(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

// Преобразует строку адреса из конфигурации в IPAddress.
static IPAddress ResolveHost(string host)
{
    if (string.IsNullOrWhiteSpace(host) || host is "0.0.0.0" or "*" or "+")
    {
        return IPAddress.Any;
    }

    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        return IPAddress.Loopback;
    }

    return IPAddress.Parse(host);
}

// Проверка токена доступа к MCP с защитой от timing-атак.
static bool IsAuthorized(HttpRequest request, string expected)
{
    string? provided = null;

    if (request.Headers.TryGetValue("X-Api-Key", out var apiKey))
    {
        provided = apiKey.ToString();
    }
    else if (request.Headers.TryGetValue("Authorization", out var auth) &&
             auth.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        provided = auth.ToString()["Bearer ".Length..].Trim();
    }

    if (string.IsNullOrEmpty(provided))
    {
        return false;
    }

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(provided),
        Encoding.UTF8.GetBytes(expected));
}
