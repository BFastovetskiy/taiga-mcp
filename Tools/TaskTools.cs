using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Инструменты управления задачами Taiga (Tasks).</summary>
[McpServerToolType]
public sealed class TaskTools : TaigaToolBase
{
    public TaskTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_tasks", ReadOnly = true)]
    [Description("Возвращает список задач с фильтрами.")]
    public Task<string> ListTasksAsync(
        [Description("ID проекта.")] int? project = null,
        [Description("ID статуса.")] int? status = null,
        [Description("ID пользовательской истории.")] int? user_story = null,
        [Description("ID milestone (спринта).")] int? milestone = null,
        [Description("ID владельца.")] int? owner = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID роли.")] int? role = null,
        [Description("ID наблюдателя (watcher).")] int? watchers = null,
        [Description("Статус закрыт (true/false).")] bool? status__is_closed = null,
        [Description("Теги через запятую.")] string? tags = null,
        [Description("Отключить пагинацию и вернуть все записи (по умолчанию true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["project"] = Q(project),
                ["status"] = Q(status),
                ["user_story"] = Q(user_story),
                ["milestone"] = Q(milestone),
                ["owner"] = Q(owner),
                ["assigned_to"] = Q(assigned_to),
                ["role"] = Q(role),
                ["watchers"] = Q(watchers),
                ["status__is_closed"] = Q(status__is_closed),
                ["tags"] = tags,
            };
            return await Client.GetAsync("tasks", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_task", ReadOnly = true)]
    [Description("Возвращает задачу по ID.")]
    public Task<string> GetTaskAsync(
        [Description("ID задачи.")] int task_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"tasks/{task_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_task_by_ref", ReadOnly = true)]
    [Description("Возвращает задачу по её номеру (ref) внутри проекта.")]
    public Task<string> GetTaskByRefAsync(
        [Description("Номер задачи (ref).")] int @ref,
        [Description("ID проекта.")] int? project = null,
        [Description("Slug проекта.")] string? project__slug = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("tasks/by_ref",
            new Dictionary<string, string?>
            {
                ["ref"] = Q(@ref),
                ["project"] = Q(project),
                ["project__slug"] = project__slug,
            },
            disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_task")]
    [Description("Создаёт задачу. Обязательны project и subject.")]
    public Task<string> CreateTaskAsync(
        [Description("ID проекта (обязательно).")] int project,
        [Description("Заголовок задачи (обязательно).")] string subject,
        [Description("Описание.")] string? description = null,
        [Description("ID пользовательской истории.")] int? user_story = null,
        [Description("ID статуса.")] int? status = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID milestone (спринта).")] int? milestone = null,
        [Description("Задача заблокирована.")] bool? is_blocked = null,
        [Description("Причина блокировки.")] string? blocked_note = null,
        [Description("Задача закрыта.")] bool? is_closed = null,
        [Description("Метка iocaine.")] bool? is_iocaine = null,
        [Description("Теги.")] string[]? tags = null,
        [Description("ID наблюдателей.")] int[]? watchers = null,
        [Description("Порядок относительно истории (us_order).")] int? us_order = null,
        [Description("Порядок на taskboard.")] int? taskboard_order = null,
        [Description("Внешняя ссылка [\"service\", \"serviceId\"].")] string[]? external_reference = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project", project),
                ("subject", subject),
                ("description", description),
                ("user_story", user_story),
                ("status", status),
                ("assigned_to", assigned_to),
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("is_iocaine", is_iocaine),
                ("tags", tags),
                ("watchers", watchers),
                ("us_order", us_order),
                ("taskboard_order", taskboard_order),
                ("external_reference", external_reference));
            return Client.PostAsync("tasks", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_task")]
    [Description("Изменяет задачу. По умолчанию PATCH; replace_all=true — PUT. Для OCC передайте version.")]
    public Task<string> UpdateTaskAsync(
        [Description("ID задачи.")] int task_id,
        [Description("Текущая версия объекта для OCC (рекомендуется).")] int? version = null,
        [Description("Полная замена (PUT) вместо частичного изменения (PATCH).")] bool replace_all = false,
        [Description("Заголовок.")] string? subject = null,
        [Description("Описание.")] string? description = null,
        [Description("ID пользовательской истории.")] int? user_story = null,
        [Description("ID статуса.")] int? status = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID milestone (спринта).")] int? milestone = null,
        [Description("Задача заблокирована.")] bool? is_blocked = null,
        [Description("Причина блокировки.")] string? blocked_note = null,
        [Description("Задача закрыта.")] bool? is_closed = null,
        [Description("Метка iocaine.")] bool? is_iocaine = null,
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
                ("user_story", user_story),
                ("status", status),
                ("assigned_to", assigned_to),
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("is_iocaine", is_iocaine),
                ("tags", tags),
                ("watchers", watchers));
            var path = $"tasks/{task_id}";
            return replace_all ? Client.PutAsync(path, body, cancellationToken) : Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_task", Destructive = true)]
    [Description("Удаляет задачу по ID.")]
    public Task<string> DeleteTaskAsync(
        [Description("ID задачи.")] int task_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_task",
            new { task_id },
            () => Client.DeleteAsync($"tasks/{task_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_bulk_create_tasks")]
    [Description("Массово создаёт задачи. Каждый элемент списка subjects — отдельная задача.")]
    public Task<string> BulkCreateTasksAsync(
        [Description("ID проекта (обязательно).")] int project_id,
        [Description("Список заголовков задач (обязательно).")] string[] subjects,
        [Description("ID статуса.")] int? status_id = null,
        [Description("ID milestone (спринта).")] int? milestone_id = null,
        [Description("ID пользовательской истории.")] int? us_id = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project_id", project_id),
                ("status_id", status_id),
                ("milestone_id", milestone_id),
                ("us_id", us_id),
                ("bulk_tasks", string.Join('\n', subjects)));
            return Client.PostAsync("tasks/bulk_create", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_watch_task")]
    [Description("Подписаться на уведомления по задаче.")]
    public Task<string> WatchTaskAsync(
        [Description("ID задачи.")] int task_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"tasks/{task_id}/watch", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unwatch_task")]
    [Description("Отписаться от уведомлений по задаче.")]
    public Task<string> UnwatchTaskAsync(
        [Description("ID задачи.")] int task_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"tasks/{task_id}/unwatch", ct: cancellationToken));
    }
}
