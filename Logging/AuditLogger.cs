using System.Text.Json;
using System.Text.Json.Nodes;
using TaigaMcp.Configuration;

namespace TaigaMcp.Logging;

/// <summary>
/// Аудит-лог вызовов деструктивных инструментов MCP.
///
/// Каждый вызов фиксируется структурированной записью в <see cref="ILogger"/>
/// (категория "TaigaMcp.Audit") и, если задан <see cref="AuditOptions.FilePath"/>,
/// дополнительно дописывается JSON-строкой в файл (формат JSON Lines).
/// </summary>
public sealed class AuditLogger
{
    // Аудит-файл: компактные однострочные записи, кириллица не экранируется.
    private static readonly JsonSerializerOptions FileJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private readonly ILogger<AuditLogger> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditOptions _options;
    private readonly object _fileLock = new();

    public AuditLogger(ILogger<AuditLogger> logger, IHttpContextAccessor httpContextAccessor, AuditOptions options)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _options = options;
    }

    /// <summary>
    /// Записывает результат вызова деструктивного инструмента.
    /// </summary>
    /// <param name="tool">Имя инструмента MCP (например, taiga_delete_project).</param>
    /// <param name="arguments">Аргументы вызова (анонимный объект или словарь).</param>
    /// <param name="success">Успешно ли завершился вызов.</param>
    /// <param name="error">Текст ошибки при неуспехе.</param>
    public void Record(string tool, object? arguments, bool success, string? error)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var clientIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var argsNode = SerializeArguments(arguments);

        _logger.LogInformation(
            "AUDIT tool={Tool} success={Success} clientIp={ClientIp} error={Error} arguments={Arguments}",
            tool,
            success,
            clientIp ?? "unknown",
            error ?? "-",
            argsNode?.ToJsonString() ?? "{}");

        WriteEntry(new AuditEntry(DateTimeOffset.UtcNow, tool, success, clientIp, error, argsNode));
    }

    private static JsonNode? SerializeArguments(object? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        if (arguments is JsonNode node)
        {
            return node.DeepClone();
        }

        return JsonSerializer.SerializeToNode(arguments);
    }

    private void WriteEntry(AuditEntry entry)
    {
        if (string.IsNullOrWhiteSpace(_options.FilePath))
        {
            return;
        }

        try
        {
            var path = ResolvePath(_options.FilePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = JsonSerializer.Serialize(entry, FileJsonOptions);
            lock (_fileLock)
            {
                RotateIfNeeded(path);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // Ошибка записи аудит-лога не должна ломать выполнение инструмента.
            _logger.LogWarning(ex, "Не удалось записать аудит-лог в файл {FilePath}", _options.FilePath);
        }
    }

    /// <summary>
    /// Ротирует аудит-файл при достижении <see cref="AuditOptions.MaxFileSizeMB"/>:
    /// сдвигает архивные копии (.1 … .N), удаляет самую старую и создаёт новый файл.
    /// Вызывается под <c>_fileLock</c>.
    /// </summary>
    private void RotateIfNeeded(string path)
    {
        if (_options.MaxFileSizeMB <= 0 || !File.Exists(path))
        {
            return;
        }

        var maxBytes = (long)_options.MaxFileSizeMB * 1024 * 1024;
        if (new FileInfo(path).Length < maxBytes)
        {
            return;
        }

        var maxBackups = Math.Max(1, _options.MaxBackupFiles);

        var oldest = $"{path}.{maxBackups}";
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = _options.MaxBackupFiles - 1; index >= 1; index--)
        {
            var source = $"{path}.{index}";
            if (File.Exists(source))
            {
                File.Move(source, $"{path}.{index + 1}", overwrite: true);
            }
        }

        File.Move(path, path + ".1", overwrite: true);
        _logger.LogInformation(
            "Аудит-лог {FilePath} достиг {MaxSizeMB} МБ и ротирован ({Backups} архивных копий).",
            path,
            _options.MaxFileSizeMB,
            _options.MaxBackupFiles);
    }

    private static string ResolvePath(string path)
        => Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

    private sealed record AuditEntry(
        DateTimeOffset Timestamp,
        string Tool,
        bool Success,
        string? ClientIp,
        string? Error,
        JsonNode? Arguments);
}
