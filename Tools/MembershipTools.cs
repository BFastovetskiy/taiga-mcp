using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Элемент массового приглашения участников.</summary>
public sealed class BulkMembership
{
    [Description("ID роли, назначаемой участнику.")]
    public int role_id { get; set; }

    [Description("Логин или email приглашаемого пользователя.")]
    public string username { get; set; } = string.Empty;

    [Description("Дополнительный текст приглашения (необязательно).")]
    public string? invitation_extra_text { get; set; }
}

/// <summary>Инструменты управления участниками и приглашениями (Memberships/Invitations).</summary>
[McpServerToolType]
public sealed class MembershipTools : TaigaToolBase
{
    public MembershipTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_memberships", ReadOnly = true)]
    [Description("Возвращает список участников/приглашений. Можно фильтровать по проекту и роли.")]
    public Task<string> ListMembershipsAsync(
        [Description("ID проекта.")] int? project = null,
        [Description("ID роли.")] int? role = null,
        [Description("Отключить пагинацию и вернуть все записи (по умолчанию true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?>
            {
                ["project"] = Q(project),
                ["role"] = Q(role),
            };
            return await Client.GetAsync("memberships", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_membership", ReadOnly = true)]
    [Description("Возвращает участника/приглашение по ID.")]
    public Task<string> GetMembershipAsync(
        [Description("ID участника.")] int membership_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"memberships/{membership_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_create_membership")]
    [Description("Приглашает пользователя в проект (создаёт membership). Все параметры обязательны.")]
    public Task<string> CreateMembershipAsync(
        [Description("ID проекта (обязательно).")] int project,
        [Description("ID роли, назначаемой участнику (обязательно).")] int role,
        [Description("Логин или email приглашаемого пользователя (обязательно).")] string username,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project", project),
                ("role", role),
                ("username", username));
            return Client.PostAsync("memberships", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_bulk_create_memberships")]
    [Description("Массово приглашает пользователей в проект.")]
    public Task<string> BulkCreateMembershipsAsync(
        [Description("ID проекта (обязательно).")] int project_id,
        [Description("Список приглашаемых: role_id, username, invitation_extra_text (обязательно).")] BulkMembership[] bulk_memberships,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("project_id", project_id),
                ("bulk_memberships", bulk_memberships));
            return Client.PostAsync("memberships/bulk_create", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_membership")]
    [Description("Изменяет участника/приглашение (например, меняет роль).")]
    public Task<string> UpdateMembershipAsync(
        [Description("ID участника.")] int membership_id,
        [Description("ID новой роли.")] int? role = null,
        [Description("Текущая версия объекта для OCC (необязательно).")] int? version = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("role", role),
                ("version", version));
            return Client.PatchAsync($"memberships/{membership_id}", body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_membership", Destructive = true)]
    [Description("Удаляет участника или отменяет приглашение.")]
    public Task<string> DeleteMembershipAsync(
        [Description("ID участника/приглашения.")] int membership_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_membership",
            new { membership_id },
            () => Client.DeleteAsync($"memberships/{membership_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_resend_invitation")]
    [Description("Повторно отправляет приглашение участнику.")]
    public Task<string> ResendInvitationAsync(
        [Description("ID участника/приглашения.")] int membership_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.PostAsync($"memberships/{membership_id}/resend_invitation", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_invitation", ReadOnly = true)]
    [Description("Возвращает приглашение по его токену (UUID).")]
    public Task<string> GetInvitationAsync(
        [Description("UUID приглашения.")] string invitation_uuid,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"invitations/{invitation_uuid}", disablePagination: null, ct: cancellationToken));
    }
}
