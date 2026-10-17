using System.Net;
using System.Reflection;
using ModelContextProtocol.Server;
using TaigaMcp.Configuration;
using TaigaMcp.Tests.Support;
using TaigaMcp.Tools;
using Xunit;

namespace TaigaMcp.Tests;

public class ToolsTests
{
    [Fact]
    public async Task ReadTool_ReturnsResponseBody()
    {
        using var harness = new ToolHarness((_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.OK, """[{"id":1}]"""));
        var tools = new ProjectTools(harness.Client, harness.Audit, harness.Metrics);

        var result = await tools.ListProjectsAsync();

        Assert.Contains("\"id\": 1", result, StringComparison.Ordinal);
        var request = Assert.Single(harness.Handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("projects", request.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateTool_SendsPostBodyWithoutNulls()
    {
        using var harness = new ToolHarness((_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.Created, """{"id":5}"""));
        var tools = new ProjectTools(harness.Client, harness.Audit, harness.Metrics);

        var result = await tools.CreateProjectAsync("Проект", "Описание");

        var request = Assert.Single(harness.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Contains("\"name\":\"Проект\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"description\":\"Описание\"", request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("null", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"id\": 5", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateTool_DefaultUsesPatch()
    {
        using var harness = new ToolHarness((_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"id":1}"""));
        var tools = new ProjectTools(harness.Client, harness.Audit, harness.Metrics);

        await tools.UpdateProjectAsync(1, name: "Новое");

        var request = Assert.Single(harness.Handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
    }

    [Fact]
    public async Task ApiError_IsFormattedForClientAndCountedAsError()
    {
        using var harness = new ToolHarness((_, _) =>
            FakeHttpMessageHandler.Json(HttpStatusCode.Forbidden, """{"detail":"nope"}"""));
        var tools = new ProjectTools(harness.Client, harness.Audit, harness.Metrics);

        var result = await tools.GetProjectStatsAsync(3);

        Assert.Contains("Ошибка Taiga API", result, StringComparison.Ordinal);
        Assert.Contains("403", result, StringComparison.Ordinal);
        Assert.Contains("nope", result, StringComparison.Ordinal);

        var exposition = await harness.ExportMetricsAsync();
        Assert.Contains("status=\"error\"", exposition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DestructiveTool_WritesAuditAndSuccessMetric()
    {
        using var harness = new ToolHarness(
            (_, _) => FakeHttpMessageHandler.Status(HttpStatusCode.NoContent),
            auditOptions: new AuditOptions { Enabled = true, FilePath = null });
        var tools = new ProjectTools(harness.Client, harness.Audit, harness.Metrics);

        await tools.DeleteProjectAsync(7);

        Assert.Contains(
            harness.CapturingLogger.Messages,
            m => m.Contains("taiga_delete_project", StringComparison.Ordinal));
        Assert.Contains(
            harness.CapturingLogger.Messages,
            m => m.Contains("AUDIT", StringComparison.Ordinal));

        var exposition = await harness.ExportMetricsAsync();
        Assert.Contains("tool=\"taiga_delete_project\",status=\"success\"", exposition, StringComparison.Ordinal);
    }

    [Fact]
    public void AllToolMethods_HaveUniqueNamesAndAreInstanceMembers()
    {
        var tools = typeof(TaigaToolBase).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(t => t
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => (Method: m, Attribute: m.GetCustomAttribute<McpServerToolAttribute>())))
            .Where(x => x.Attribute is not null)
            .ToList();

        Assert.Equal(72, tools.Count);
        Assert.Equal(tools.Count, tools.Select(x => x.Attribute!.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(tools, x => Assert.False(x.Method.IsStatic));
        Assert.All(tools, x => Assert.False(string.IsNullOrWhiteSpace(x.Attribute!.Name)));
    }
}
