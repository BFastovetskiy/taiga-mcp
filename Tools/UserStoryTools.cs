using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Инструменты управления пользовательскими историями Taiga (User Stories).</summary>
[McpServerToolType]
public sealed class UserStoryTools : TaigaToolBase
{
    public UserStoryTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_userstories", ReadOnly = true)]
    [Description("Возвращает список пользовательских историй с фильтрами.")]
    public Task<string> ListUserStoriesAsync(
        [Description("ID проекта.")] int? project = null,
        [Description("ID milestone (спринта).")] int? milestone = null,
        [Description("История связана с milestone или нет (true/false).")] bool? milestone__isnull = null,
        [Description("ID статуса.")] int? status = null,
        [Description("Статус закрыт (true/false).")] bool? status__is_closed = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID эпика.")] int? epic = null,
        [Description("ID роли.")] int? role = null,
        [Description("ID наблюдателя (watcher).")] int? watchers = null,
        [Description("Теги через запятую.")] string? tags = null,
        [Description("Отключить пагинацию и вернуть все записи (по умолчанию true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["project"] = Q(project),
                ["milestone"] = Q(milestone),
                ["milestone__isnull"] = Q(milestone__isnull),
                ["status"] = Q(status),
                ["status__is_closed"] = Q(status__is_closed),
                ["assigned_to"] = Q(assigned_to),
                ["epic"] = Q(epic),
                ["role"] = Q(role),
                ["watchers"] = Q(watchers),
                ["tags"] = tags,
            };
            return await Client.GetAsync("userstories", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_userstory", ReadOnly = true)]
    [Description("Возвращает пользовательскую историю по ID.")]
    public Task<string> GetUserStoryAsync(
        [Description("ID истории.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"userstories/{user_story_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_userstory_by_ref", ReadOnly = true)]
    [Description("Возвращает пользовательскую историю по её номеру (ref) внутри проекта.")]
    public Task<string> GetUserStoryByRefAsync(
        [Description("Номер истории (ref).")] int @ref,
        [Description("ID проекта.")] int? project = null,
        [Description("Slug проекта.")] string? project__slug = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("userstories/by_ref",
            new Dictionary<string, string?>
            {
                ["ref"] = Q(@ref),
                ["project"] = Q(project),
                ["project__slug"] = project__slug,
            },
            disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_userstory")]
    [Description("Создаёт пользовательскую историю. Обязательны project и subject.")]
    public Task<string> CreateUserStoryAsync(
        [Description("ID проекта (обязательно).")] int project,
        [Description("Заголовок истории (обязательно).")] string subject,
        [Description("Описание.")] string? description = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID статуса.")] int? status = null,
        [Description("ID milestone (спринта).")] int? milestone = null,
        [Description("История заблокирована.")] bool? is_blocked = null,
        [Description("Причина блокировки.")] string? blocked_note = null,
        [Description("История закрыта.")] bool? is_closed = null,
        [Description("Требование клиента.")] bool? client_requirement = null,
        [Description("Требование команды.")] bool? team_requirement = null,
        [Description("Очки (points): словарь вида {\"ID_роли\": значение}.")] Dictionary<string, double>? points = null,
        [Description("Теги.")] string[]? tags = null,
        [Description("ID наблюдателей.")] int[]? watchers = null,
        [Description("Порядок в backlog.")] int? backlog_order = null,
        [Description("Порядок в спринте.")] int? sprint_order = null,
        [Description("Порядок в kanban.")] int? kanban_order = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project", project),
                ("subject", subject),
                ("description", description),
                ("assigned_to", assigned_to),
                ("status", status),
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("client_requirement", client_requirement),
                ("team_requirement", team_requirement),
                ("points", points),
                ("tags", tags),
                ("watchers", watchers),
                ("backlog_order", backlog_order),
                ("sprint_order", sprint_order),
                ("kanban_order", kanban_order));
            return Client.PostAsync("userstories", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_userstory")]
    [Description("Изменяет пользовательскую историю. По умолчанию PATCH; replace_all=true — PUT. Для OCC передайте version.")]
    public Task<string> UpdateUserStoryAsync(
        [Description("ID истории.")] int user_story_id,
        [Description("Текущая версия объекта для OCC (рекомендуется).")] int? version = null,
        [Description("Полная замена (PUT) вместо частичного изменения (PATCH).")] bool replace_all = false,
        [Description("Заголовок.")] string? subject = null,
        [Description("Описание.")] string? description = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID статуса.")] int? status = null,
        [Description("ID milestone (спринта).")] int? milestone = null,
        [Description("История заблокирована.")] bool? is_blocked = null,
        [Description("Причина блокировки.")] string? blocked_note = null,
        [Description("История закрыта.")] bool? is_closed = null,
        [Description("Требование клиента.")] bool? client_requirement = null,
        [Description("Требование команды.")] bool? team_requirement = null,
        [Description("Очки (points): словарь {\"ID_роли\": значение}.")] Dictionary<string, double>? points = null,
        [Description("Теги.")] string[]? tags = null,
        [Description("ID наблюдателей.")] int[]? watchers = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("version", version),
                ("subject", subject),
                ("description", description),
                ("assigned_to", assigned_to),
                ("status", status),
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("client_requirement", client_requirement),
                ("team_requirement", team_requirement),
                ("points", points),
                ("tags", tags),
                ("watchers", watchers));
            var path = $"userstories/{user_story_id}";
            return replace_all ? Client.PutAsync(path, body, cancellationToken) : Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_userstory", Destructive = true)]
    [Description("Удаляет пользовательскую историю по ID.")]
    public Task<string> DeleteUserStoryAsync(
        [Description("ID истории.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_userstory",
            new { user_story_id },
            () => Client.DeleteAsync($"userstories/{user_story_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_bulk_create_userstories")]
    [Description("Массово создаёт истории. Каждый элемент списка subjects — отдельная история.")]
    public Task<string> BulkCreateUserStoriesAsync(
        [Description("ID проекта (обязательно).")] int project_id,
        [Description("Список заголовков историй (обязательно).")] string[] subjects,
        [Description("ID статуса.")] int? status_id = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project_id", project_id),
                ("status_id", status_id),
                ("bulk_stories", string.Join('\n', subjects)));
            return Client.PostAsync("userstories/bulk_create", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_watch_userstory")]
    [Description("Подписаться на уведомления по истории.")]
    public Task<string> WatchUserStoryAsync(
        [Description("ID истории.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"userstories/{user_story_id}/watch", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unwatch_userstory")]
    [Description("Отписаться от уведомлений по истории.")]
    public Task<string> UnwatchUserStoryAsync(
        [Description("ID истории.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"userstories/{user_story_id}/unwatch", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_vote_userstory")]
    [Description("Голосует за историю (upvote).")]
    public Task<string> VoteUserStoryAsync(
        [Description("ID истории.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"userstories/{user_story_id}/upvote", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unvote_userstory")]
    [Description("Отменяет голос за историю (downvote).")]
    public Task<string> UnvoteUserStoryAsync(
        [Description("ID истории.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"userstories/{user_story_id}/downvote", ct: cancellationToken));
    }
}
