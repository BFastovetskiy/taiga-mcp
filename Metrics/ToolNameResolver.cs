using System.Collections.Concurrent;
using System.Reflection;
using ModelContextProtocol.Server;

namespace TaigaMcp.Metrics;

/// <summary>
/// Определяет имя MCP-инструмента (значение <c>McpServerToolAttribute.Name</c>)
/// по классу и имени метода инструмента. Результат кэшируется.
/// </summary>
public static class ToolNameResolver
{
    private static readonly ConcurrentDictionary<(Type Type, string Method), string> Cache = new();

    /// <summary>Возвращает имя MCP-инструмента для метода класса.</summary>
    public static string Resolve(Type toolClass, string methodName)
        => Cache.GetOrAdd((toolClass, methodName), static key => ResolveCore(key.Type, key.Method));

    private static string ResolveCore(Type toolClass, string methodName)
    {
        var method = toolClass.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        var attribute = method?.GetCustomAttribute<McpServerToolAttribute>();
        return attribute?.Name ?? methodName;
    }
}
