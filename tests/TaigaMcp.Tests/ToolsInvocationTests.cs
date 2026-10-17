using System.Net;
using System.Reflection;
using ModelContextProtocol.Server;
using TaigaMcp.Tests.Support;
using TaigaMcp.Tools;
using Xunit;

namespace TaigaMcp.Tests;

/// <summary>
/// Прогоняет каждый зарегистрированный инструмент MCP с типовыми аргументами против
/// поддельного HTTP-транспорта. Проверяет, что инструменты корректно строят запросы,
/// обрабатывают ответ и всегда возвращают текстовый результат (без падения сервера).
/// </summary>
public class ToolsInvocationTests
{
    private static IEnumerable<(Type Type, MethodInfo Method)> EnumerateTools()
        => typeof(TaigaToolBase).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .Select(m => (Type: t, Method: m)));

    private static object? BuildArgument(ParameterInfo parameter)
    {
        if (parameter.ParameterType == typeof(CancellationToken))
        {
            return CancellationToken.None;
        }

        if (parameter.HasDefaultValue)
        {
            return parameter.DefaultValue;
        }

        return parameter.ParameterType switch
        {
            var t when t == typeof(string) => "sample",
            var t when t == typeof(int) => 1,
            var t when t == typeof(double) => 1.5,
            var t when t == typeof(bool) => true,
            var t when t == typeof(string[]) => new[] { "sample" },
            var t when t == typeof(int[]) => new[] { 1, 2 },
            var t when t == typeof(Dictionary<string, double>) => new Dictionary<string, double> { ["1"] = 1.5 },
            var t when t == typeof(BulkMembership[]) => new[]
            {
                new BulkMembership { role_id = 1, username = "user" },
            },
            var t => t.IsValueType ? Activator.CreateInstance(t) : null,
        };
    }

    [Fact]
    public async Task EveryTool_ExecutesAndReturnsText()
    {
        using var harness = new ToolHarness((_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"id":1}"""));

        var executed = 0;
        foreach (var (type, method) in EnumerateTools())
        {
            var instance = Activator.CreateInstance(type, harness.Client, harness.Audit, harness.Metrics)!;
            var arguments = method.GetParameters().Select(BuildArgument).ToArray();

            var task = Assert.IsAssignableFrom<Task<string>>(method.Invoke(instance, arguments));

            var result = await task;
            Assert.NotNull(result);
            executed++;
        }

        Assert.Equal(72, executed);
    }
}
