using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Инструменты управления ролями проекта Taiga (Roles).</summary>
[McpServerToolType]
public sealed class RoleTools : TaigaToolBase
{
    public RoleTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_roles", ReadOnly = true)]
    [Description("Возвращает список ролей. Можно отфильтровать по проекту.")]
    public Task<string> ListRolesAsync(
        [Description("ID проекта.")] int? project = null,
        [Description("Отключить пагинацию и вернуть все записи (по умолчанию true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?> { ["project"] = Q(project) };
            return await Client.GetAsync("roles", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_role", ReadOnly = true)]
    [Description("Возвращает роль по ID.")]
    public Task<string> GetRoleAsync(
        [Description("ID роли.")] int role_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"roles/{role_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_role")]
    [Description("Создаёт роль в проекте. Обязательны name и project.")]
    public Task<string> CreateRoleAsync(
        [Description("ID проекта (обязательно).")] int project,
        [Description("Название роли (обязательно).")] string name,
        [Description("Порядок роли.")] int? order = null,
        [Description("Роль учитывает оценки (true/false).")] bool? computable = null,
        [Description("Список разрешений, например: view_us, view_project, add_us и т.п.")] string[]? permissions = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project", project),
                ("name", name),
                ("order", order),
                ("computable", computable),
                ("permissions", permissions));
            return Client.PostAsync("roles", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_role")]
    [Description("Изменяет роль. По умолчанию PATCH; replace_all=true — PUT.")]
    public Task<string> UpdateRoleAsync(
        [Description("ID роли.")] int role_id,
        [Description("Текущая версия объекта для OCC (необязательно).")] int? version = null,
        [Description("Полная замена (PUT) вместо частичного изменения (PATCH).")] bool replace_all = false,
        [Description("Новое название роли.")] string? name = null,
        [Description("Порядок роли.")] int? order = null,
        [Description("Роль учитывает оценки (true/false).")] bool? computable = null,
        [Description("Список разрешений.")] string[]? permissions = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("version", version),
                ("name", name),
                ("order", order),
                ("computable", computable),
                ("permissions", permissions));
            var path = $"roles/{role_id}";
            return replace_all ? Client.PutAsync(path, body, cancellationToken) : Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_role", Destructive = true)]
    [Description("Удаляет роль. Можно указать move_to — ID роли, на которую переназначить пользователей.")]
    public Task<string> DeleteRoleAsync(
        [Description("ID роли.")] int role_id,
        [Description("ID роли, на которую переназначить пользователей удаляемой роли.")] int? move_to = null,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_role",
            new { role_id, move_to },
            () =>
            {
                var query = new Dictionary<string, string?> { ["moveTo"] = Q(move_to) };
                return Client.DeleteAsync($"roles/{role_id}", query, cancellationToken);
            });
    }
}
