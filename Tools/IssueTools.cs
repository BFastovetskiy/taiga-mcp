using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Инструменты управления проблемами Taiga (Issues).</summary>
[McpServerToolType]
public sealed class IssueTools : TaigaToolBase
{
    public IssueTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_issues", ReadOnly = true)]
    [Description("Возвращает список проблем (issues) с фильтрами.")]
    public Task<string> ListIssuesAsync(
        [Description("ID проекта.")] int? project = null,
        [Description("ID статуса.")] int? status = null,
        [Description("ID критичности (severity).")] int? severity = null,
        [Description("ID приоритета (priority).")] int? priority = null,
        [Description("ID типа проблемы (type).")] int? type = null,
        [Description("ID владельца.")] int? owner = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID роли.")] int? role = null,
        [Description("ID наблюдателя (watcher).")] int? watchers = null,
        [Description("Статус закрыт (true/false).")] bool? status__is_closed = null,
        [Description("Теги через запятую.")] string? tags = null,
        [Description("Поле сортировки: type, severity, status, priority, created_date, modified_date, owner, assigned_to, subject.")] string? order_by = null,
        [Description("Отключить пагинацию и вернуть все записи (по умолчанию true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["project"] = Q(project),
                ["status"] = Q(status),
                ["severity"] = Q(severity),
                ["priority"] = Q(priority),
                ["type"] = Q(type),
                ["owner"] = Q(owner),
                ["assigned_to"] = Q(assigned_to),
                ["role"] = Q(role),
                ["watchers"] = Q(watchers),
                ["status__is_closed"] = Q(status__is_closed),
                ["tags"] = tags,
                ["order_by"] = order_by,
            };
            return await Client.GetAsync("issues", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_issue", ReadOnly = true)]
    [Description("Возвращает проблему (issue) по ID.")]
    public Task<string> GetIssueAsync(
        [Description("ID проблемы.")] int issue_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"issues/{issue_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_issue_by_ref", ReadOnly = true)]
    [Description("Возвращает проблему (issue) по её номеру (ref) внутри проекта.")]
    public Task<string> GetIssueByRefAsync(
        [Description("Номер проблемы (ref).")] int @ref,
        [Description("ID проекта.")] int? project = null,
        [Description("Slug проекта.")] string? project__slug = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("issues/by_ref",
            new Dictionary<string, string?>
            {
                ["ref"] = Q(@ref),
                ["project"] = Q(project),
                ["project__slug"] = project__slug,
            },
            disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_issue")]
    [Description("Создаёт проблему (issue). Обязательны project и subject.")]
    public Task<string> CreateIssueAsync(
        [Description("ID проекта (обязательно).")] int project,
        [Description("Заголовок проблемы (обязательно).")] string subject,
        [Description("Описание.")] string? description = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID статуса.")] int? status = null,
        [Description("ID критичности (severity).")] int? severity = null,
        [Description("ID приоритета (priority).")] int? priority = null,
        [Description("ID типа проблемы (type).")] int? type = null,
        [Description("ID milestone (спринта).")] int? milestone = null,
        [Description("Проблема заблокирована.")] bool? is_blocked = null,
        [Description("Причина блокировки.")] string? blocked_note = null,
        [Description("Проблема закрыта.")] bool? is_closed = null,
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
                ("severity", severity),
                ("priority", priority),
                ("type", type),
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("tags", tags),
                ("watchers", watchers));
            return Client.PostAsync("issues", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_issue")]
    [Description("Изменяет проблему (issue). По умолчанию PATCH; replace_all=true — PUT. Для OCC передайте version.")]
    public Task<string> UpdateIssueAsync(
        [Description("ID проблемы.")] int issue_id,
        [Description("Текущая версия объекта для OCC (рекомендуется).")] int? version = null,
        [Description("Полная замена (PUT) вместо частичного изменения (PATCH).")] bool replace_all = false,
        [Description("Заголовок.")] string? subject = null,
        [Description("Описание.")] string? description = null,
        [Description("ID назначенного пользователя.")] int? assigned_to = null,
        [Description("ID статуса.")] int? status = null,
        [Description("ID критичности (severity).")] int? severity = null,
        [Description("ID приоритета (priority).")] int? priority = null,
        [Description("ID типа проблемы (type).")] int? type = null,
        [Description("ID milestone (спринта).")] int? milestone = null,
        [Description("Проблема заблокирована.")] bool? is_blocked = null,
        [Description("Причина блокировки.")] string? blocked_note = null,
        [Description("Проблема закрыта.")] bool? is_closed = null,
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
                ("severity", severity),
                ("priority", priority),
                ("type", type),
                ("milestone", milestone),
                ("is_blocked", is_blocked),
                ("blocked_note", blocked_note),
                ("is_closed", is_closed),
                ("tags", tags),
                ("watchers", watchers));
            var path = $"issues/{issue_id}";
            return replace_all ? Client.PutAsync(path, body, cancellationToken) : Client.PatchAsync(path, body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_issue", Destructive = true)]
    [Description("Удаляет проблему (issue) по ID.")]
    public Task<string> DeleteIssueAsync(
        [Description("ID проблемы.")] int issue_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_issue",
            new { issue_id },
            () => Client.DeleteAsync($"issues/{issue_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_watch_issue")]
    [Description("Подписаться на уведомления по проблеме.")]
    public Task<string> WatchIssueAsync(
        [Description("ID проблемы.")] int issue_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"issues/{issue_id}/watch", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_unwatch_issue")]
    [Description("Отписаться от уведомлений по проблеме.")]
    public Task<string> UnwatchIssueAsync(
        [Description("ID проблемы.")] int issue_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"issues/{issue_id}/unwatch", ct: cancellationToken));
    }
}
