using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Инструменты управления эпиками Taiga (Epics).</summary>
[McpServerToolType]
public sealed class EpicTools : TaigaToolBase
{
    public EpicTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_epics", ReadOnly = true)]
    [Description("Возвращает список эпиков с фильтрами.")]
    public Task<string> ListEpicsAsync(
        [Description("ID проекта.")] int? project = null,
        [Description("Slug проекта.")] string? project__slug = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("Статус эпика закрыт (true/false).")] bool? status__is_closed = null,
        [Description("Отключить пагинацию и вернуть все записи (по умолчанию true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["project"] = Q(project),
                ["project__slug"] = project__slug,
                ["assigned_to"] = Q(assigned_to),
                ["status__is_closed"] = Q(status__is_closed),
            };
            return await Client.GetAsync("epics", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_epic", ReadOnly = true)]
    [Description("Возвращает эпик по ID.")]
    public Task<string> GetEpicAsync(
        [Description("ID эпика.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"epics/{epic_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_epic_by_ref", ReadOnly = true)]
    [Description("Возвращает эпик по его номеру (ref) внутри проекта.")]
    public Task<string> GetEpicByRefAsync(
        [Description("Номер эпика (ref).")] int @ref,
        [Description("ID проекта.")] int? project = null,
        [Description("Slug проекта.")] string? project__slug = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("epics/by_ref",
            new Dictionary<string, string?>
            {
                ["ref"] = Q(@ref),
                ["project"] = Q(project),
                ["project__slug"] = project__slug,
            },
            disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_epic")]
    [Description("Создаёт эпик. Обязательны project и subject.")]
    public Task<string> CreateEpicAsync(
        [Description("ID проекта (обязательно).")] int project,
        [Description("Заголовок эпика (обязательно).")] string subject,
        [Description("Описание.")] string? description = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID статуса эпика.")] int? status = null,
        [Description("Цвет в формате HEX, например #ABCABC.")] string? color = null,
        [Description("Эпик заблокирован.")] bool? is_blocked = null,
        [Description("Причина блокировки.")] string? blocked_note = null,
        [Description("Эпик закрыт.")] bool? is_closed = null,
        [Description("Требование клиента.")] bool? client_requirement = null,
        [Description("Требование команды.")] bool? team_requirement = null,
        [Description("Порядок эпика (epics_order).")] int? epics_order = null,
        [Description("Теги.")] string[]? tags = null,
        [Description("ID наблюдателей.")] int[]? watchers = null,
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
                ("color", color),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("client_requirement", client_requirement),
                ("team_requirement", team_requirement),
                ("epics_order", epics_order),
                ("tags", tags),
                ("watchers", watchers));
            return Client.PostAsync("epics", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_epic")]
    [Description("Изменяет эпик. По умолчанию PATCH; replace_all=true — PUT. Для OCC передайте version.")]
    public Task<string> UpdateEpicAsync(
        [Description("ID эпика.")] int epic_id,
        [Description("Текущая версия объекта для OCC (рекомендуется).")] int? version = null,
        [Description("Полная замена (PUT) вместо частичного изменения (PATCH).")] bool replace_all = false,
        [Description("Заголовок.")] string? subject = null,
        [Description("Описание.")] string? description = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID статуса эпика.")] int? status = null,
        [Description("Цвет в формате HEX.")] string? color = null,
        [Description("Эпик заблокирован.")] bool? is_blocked = null,
        [Description("Причина блокировки.")] string? blocked_note = null,
        [Description("Эпик закрыт.")] bool? is_closed = null,
        [Description("Требование клиента.")] bool? client_requirement = null,
        [Description("Требование команды.")] bool? team_requirement = null,
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
                ("color", color),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("client_requirement", client_requirement),
                ("team_requirement", team_requirement),
                ("tags", tags),
                ("watchers", watchers));
            var path = $"epics/{epic_id}";
            return replace_all ? Client.PutAsync(path, body, cancellationToken) : Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_epic", Destructive = true)]
    [Description("Удаляет эпик по ID.")]
    public Task<string> DeleteEpicAsync(
        [Description("ID эпика.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_epic",
            new { epic_id },
            () => Client.DeleteAsync($"epics/{epic_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_bulk_create_epics")]
    [Description("Массово создаёт эпики. Каждый элемент списка subjects — отдельный эпик.")]
    public Task<string> BulkCreateEpicsAsync(
        [Description("ID проекта (обязательно).")] int project_id,
        [Description("Список заголовков эпиков (обязательно).")] string[] subjects,
        [Description("ID статуса эпика.")] int? status_id = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project_id", project_id),
                ("status_id", status_id),
                ("bulk_epics", string.Join('\n', subjects)));
            return Client.PostAsync("epics/bulk_create", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_list_epic_related_userstories", ReadOnly = true)]
    [Description("Возвращает список пользовательских историй, связанных с эпиком.")]
    public Task<string> ListEpicRelatedUserStoriesAsync(
        [Description("ID эпика.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"epics/{epic_id}/related_userstories", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_epic_related_userstory")]
    [Description("Связывает пользовательскую историю с эпиком.")]
    public Task<string> CreateEpicRelatedUserStoryAsync(
        [Description("ID эпика.")] int epic_id,
        [Description("ID пользовательской истории.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("epic", epic_id),
                ("user_story", user_story_id));
            return Client.PostAsync($"epics/{epic_id}/related_userstories", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_epic_related_userstory", Destructive = true)]
    [Description("Удаляет связь пользовательской истории с эпиком.")]
    public Task<string> DeleteEpicRelatedUserStoryAsync(
        [Description("ID эпика.")] int epic_id,
        [Description("ID пользовательской истории.")] int user_story_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_epic_related_userstory",
            new { epic_id, user_story_id },
            () => Client.DeleteAsync($"epics/{epic_id}/related_userstories/{user_story_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_vote_epic")]
    [Description("Голосует за эпик (upvote).")]
    public Task<string> VoteEpicAsync(
        [Description("ID эпика.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"epics/{epic_id}/upvote", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_watch_epic")]
    [Description("Подписаться на уведомления по эпику.")]
    public Task<string> WatchEpicAsync(
        [Description("ID эпика.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"epics/{epic_id}/watch", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unwatch_epic")]
    [Description("Отписаться от уведомлений по эпику.")]
    public Task<string> UnwatchEpicAsync(
        [Description("ID эпика.")] int epic_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"epics/{epic_id}/unwatch", ct: cancellationToken));
    }
}
