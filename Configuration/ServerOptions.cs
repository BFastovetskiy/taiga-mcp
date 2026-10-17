namespace TaigaMcp.Configuration;

/// <summary>
/// Настройки веб-сервера (адрес и порт прослушивания).
/// Берутся из секции "Server" и могут быть переопределены переменными MCP_HOST / MCP_PORT.
/// </summary>
public sealed class ServerOptions
{
    /// <summary>
    /// Адрес прослушивания.
    /// "0.0.0.0" (по умолчанию) — все сетевые интерфейсы, "127.0.0.1" — только локально,
    /// также поддерживаются "localhost", "*", "+" и конкретный IP.
    /// </summary>
    public string Host { get; set; } = "0.0.0.0";

    /// <summary>Порт прослушивания.</summary>
    public int Port { get; set; } = 5000;
}
