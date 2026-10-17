namespace TaigaMcp.Configuration;

/// <summary>
/// Настройки аудит-лога деструктивных операций.
/// Берутся из секции "Audit" и могут быть переопределены переменными
/// AUDIT_ENABLED / AUDIT_FILE_PATH.
/// </summary>
public sealed class AuditOptions
{
    /// <summary>Включён ли аудит-лог.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Путь к файлу аудит-лога (формат JSON Lines).
    /// Если пусто — записи идут только в ILogger (категория "TaigaMcp.Audit").
    /// Относительный путь разрешается относительно каталога сборки.
    /// </summary>
    public string? FilePath { get; set; } = "logs/audit.log";

    /// <summary>
    /// Максимальный размер аудит-файла в мегабайтах до ротации. 0 — ротация отключена.
    /// По умолчанию 64 МБ.
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 64;

    /// <summary>
    /// Число архивных копий при ротации (audit.log.1 … audit.log.N).
    /// Самый старый файл удаляется.
    /// </summary>
    public int MaxBackupFiles { get; set; } = 5;
}
