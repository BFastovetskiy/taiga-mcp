using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Инструменты управления проектами Taiga (Projects).</summary>
[McpServerToolType]
public sealed class ProjectTools : TaigaToolBase
{
    public ProjectTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_projects", ReadOnly = true)]
    [Description("Возвращает список проектов Taiga. Поддерживает фильтры и сортировку.")]
    public Task<string> ListProjectsAsync(
        [Description("ID участника (member id) для фильтрации проектов, где он состоит.")] int? member = null,
        [Description("ID участников через запятую.")] string? members = null,
        [Description("Проект ищет новых участников (true/false).")] bool? is_looking_for_people = null,
        [Description("Проект отмечен как рекомендуемый (true/false).")] bool? is_featured = null,
        [Description("Активирован backlog (true/false).")] bool? is_backlog_activated = null,
        [Description("Активирован kanban (true/false).")] bool? is_kanban_activated = null,
        [Description("Поле сортировки: memberships__user_order, total_fans, total_activity, total_fans_last_week и т.п.")] string? order_by = null,
        [Description("Отключить пагинацию и вернуть все записи (по умолчанию true).")] bool? disable_pagination = null,
        [Description("Номер страницы (если пагинация включена).")] int? page = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["member"] = Q(member),
                ["members"] = members,
                ["is_looking_for_people"] = Q(is_looking_for_people),
                ["is_featured"] = Q(is_featured),
                ["is_backlog_activated"] = Q(is_backlog_activated),
                ["is_kanban_activated"] = Q(is_kanban_activated),
                ["order_by"] = order_by,
                ["page"] = Q(page),
            };
            return await Client.GetAsync("projects", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_project", ReadOnly = true)]
    [Description("Возвращает проект по его ID.")]
    public Task<string> GetProjectAsync(
        [Description("ID проекта.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"projects/{project_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_project_by_slug", ReadOnly = true)]
    [Description("Возвращает проект по его slug.")]
    public Task<string> GetProjectBySlugAsync(
        [Description("Slug проекта, например 'project-0'.")] string slug,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("projects/by_slug",
            new Dictionary<string, string?> { ["slug"] = slug },
            disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_project")]
    [Description("Создаёт новый проект Taiga. Обязательны name и description.")]
    public Task<string> CreateProjectAsync(
        [Description("Название проекта (обязательно).")] string name,
        [Description("Описание проекта (обязательно).")] string description,
        [Description("ID базового шаблона проекта (creation_template).")] int? creation_template = null,
        [Description("Активировать backlog.")] bool? is_backlog_activated = null,
        [Description("Активировать модуль Issues.")] bool? is_issues_activated = null,
        [Description("Активировать kanban.")] bool? is_kanban_activated = null,
        [Description("Сделать проект приватным.")] bool? is_private = null,
        [Description("Активировать wiki.")] bool? is_wiki_activated = null,
        [Description("Сервис видеоконференций: whereby-com, jitsi, talky или custom.")] string? videoconferences = null,
        [Description("Доп. данные для генерации ссылки видеоконференции.")] string? videoconferences_extra_data = null,
        [Description("Общее количество milestones.")] int? total_milestones = null,
        [Description("Общее количество story points.")] double? total_story_points = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("name", name),
                ("description", description),
                ("creation_template", creation_template),
                ("is_backlog_activated", is_backlog_activated),
                ("is_issues_activated", is_issues_activated),
                ("is_kanban_activated", is_kanban_activated),
                ("is_private", is_private),
                ("is_wiki_activated", is_wiki_activated),
                ("videoconferences", videoconferences),
                ("videoconferences_extra_data", videoconferences_extra_data),
                ("total_milestones", total_milestones),
                ("total_story_points", total_story_points));
            return Client.PostAsync("projects", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_project")]
    [Description("Изменяет проект. По умолчанию частичное изменение (PATCH); replace_all=true — полная замена (PUT).")]
    public Task<string> UpdateProjectAsync(
        [Description("ID проекта.")] int project_id,
        [Description("Текущая версия объекта для OCC (необязательно, но рекомендуется).")] int? version = null,
        [Description("Полная замена объекта (PUT) вместо частичного изменения (PATCH).")] bool replace_all = false,
        [Description("Новое название проекта.")] string? name = null,
        [Description("Новое описание проекта.")] string? description = null,
        [Description("Активировать backlog.")] bool? is_backlog_activated = null,
        [Description("Активировать модуль Issues.")] bool? is_issues_activated = null,
        [Description("Активировать kanban.")] bool? is_kanban_activated = null,
        [Description("Сделать проект приватным.")] bool? is_private = null,
        [Description("Активировать wiki.")] bool? is_wiki_activated = null,
        [Description("Сервис видеоконференций: whereby-com, jitsi, talky или custom.")] string? videoconferences = null,
        [Description("Доп. данные для видеоконференции.")] string? videoconferences_extra_data = null,
        [Description("Проект ищет новых участников.")] bool? is_looking_for_people = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("version", version),
                ("name", name),
                ("description", description),
                ("is_backlog_activated", is_backlog_activated),
                ("is_issues_activated", is_issues_activated),
                ("is_kanban_activated", is_kanban_activated),
                ("is_private", is_private),
                ("is_wiki_activated", is_wiki_activated),
                ("videoconferences", videoconferences),
                ("videoconferences_extra_data", videoconferences_extra_data),
                ("is_looking_for_people", is_looking_for_people));
            var path = $"projects/{project_id}";
            return replace_all ? Client.PutAsync(path, body, cancellationToken) : Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_project", Destructive = true)]
    [Description("Удаляет проект по ID.")]
    public Task<string> DeleteProjectAsync(
        [Description("ID проекта.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_project",
            new { project_id },
            () => Client.DeleteAsync($"projects/{project_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_project_stats", ReadOnly = true)]
    [Description("Возвращает статистику проекта.")]
    public Task<string> GetProjectStatsAsync(
        [Description("ID проекта.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"projects/{project_id}/stats", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_leave_project")]
    [Description("Покидает проект от имени текущего пользователя.")]
    public Task<string> LeaveProjectAsync(
        [Description("ID проекта.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"projects/{project_id}/leave", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_like_project")]
    [Description("Ставит «лайк» проекту.")]
    public Task<string> LikeProjectAsync(
        [Description("ID проекта.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"projects/{project_id}/like", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unlike_project")]
    [Description("Убирает «лайк» с проекта.")]
    public Task<string> UnlikeProjectAsync(
        [Description("ID проекта.")] int project_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"projects/{project_id}/unlike", ct: cancellationToken));
    }
}
