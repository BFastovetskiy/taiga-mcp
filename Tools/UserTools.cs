using System.ComponentModel;
using ModelContextProtocol.Server;
using TaigaMcp.Logging;
using TaigaMcp.Metrics;
using TaigaMcp.Taiga;

namespace TaigaMcp.Tools;

/// <summary>Инструменты управления пользователями Taiga (Users).</summary>
[McpServerToolType]
public sealed class UserTools : TaigaToolBase
{
    public UserTools(TaigaClient client, AuditLogger audit, McpMetrics metrics) : base(client, audit, metrics) { }

    [McpServerTool(Name = "taiga_list_users", ReadOnly = true)]
    [Description("Возвращает список пользователей. Можно отфильтровать по проекту.")]
    public Task<string> ListUsersAsync(
        [Description("ID проекта — вернуть только его участников.")] int? project = null,
        [Description("Отключить пагинацию и вернуть все записи (по умолчанию true).")] bool? disable_pagination = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
        {
            var query = new Dictionary<string, string?> { ["project"] = Q(project) };
            return await Client.GetAsync("users", query, disable_pagination, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_get_user", ReadOnly = true)]
    [Description("Возвращает пользователя по ID.")]
    public Task<string> GetUserAsync(
        [Description("ID пользователя.")] int user_id,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync($"users/{user_id}", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_get_current_user", ReadOnly = true)]
    [Description("Возвращает текущего аутентифицированного пользователя (users/me).")]
    public Task<string> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(() => Client.GetAsync("users/me", disablePagination: null, ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_register_user")]
    [Description("Регистрирует нового пользователя (публичная регистрация). Все параметры обязательны.")]
    public Task<string> RegisterUserAsync(
        [Description("Логин нового пользователя.")] string username,
        [Description("Пароль.")] string password,
        [Description("Email.")] string email,
        [Description("Полное имя.")] string full_name,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("type", "public"),
                ("username", username),
                ("password", password),
                ("email", email),
                ("full_name", full_name),
                ("accepted_terms", true));
            return Client.PostAsync("auth/register", body, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_update_user")]
    [Description("Изменяет профиль пользователя (PATCH).")]
    public Task<string> UpdateUserAsync(
        [Description("ID пользователя.")] int user_id,
        [Description("Текущая версия объекта для OCC (необязательно).")] int? version = null,
        [Description("Новый логин.")] string? username = null,
        [Description("Новое полное имя.")] string? full_name = null,
        [Description("Новый email.")] string? email = null,
        [Description("Биография.")] string? bio = null,
        [Description("Язык интерфейса (ID локали).")] string? lang = null,
        [Description("Часовой пояс.")] string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(
                ("version", version),
                ("username", username),
                ("full_name", full_name),
                ("email", email),
                ("bio", bio),
                ("lang", lang),
                ("timezone", timezone));
            return Client.PatchAsync($"users/{user_id}", body, cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_delete_user", Destructive = true)]
    [Description("Удаляет пользователя по ID. Требуются права администратора.")]
    public Task<string> DeleteUserAsync(
        [Description("ID пользователя.")] int user_id,
        CancellationToken cancellationToken = default)
    {
        return RunDestructiveAsync(
            "taiga_delete_user",
            new { user_id },
            () => Client.DeleteAsync($"users/{user_id}", ct: cancellationToken));
    }

    [McpServerTool(Name = "taiga_list_user_contacts", ReadOnly = true)]
    [Description("Возвращает контакты пользователя (список пользователей).")]
    public Task<string> ListUserContactsAsync(
        [Description("ID пользователя.")] int user_id,
        [Description("Строка поиска по логину, полному имени или email.")] string? q = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var query = new Dictionary<string, string?> { ["q"] = q };
            return Client.GetAsync($"users/{user_id}/contacts", query, disablePagination: null, ct: cancellationToken);
        });
    }

    [McpServerTool(Name = "taiga_request_password_recovery")]
    [Description("Запрашивает восстановление пароля пользователя.")]
    public Task<string> RequestPasswordRecoveryAsync(
        [Description("Логин или email пользователя.")] string username,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(() =>
        {
            var body = JsonBody.Object(("username", username));
            return Client.PostAsync("users/password_recovery", body, ct: cancellationToken);
        });
    }
}
