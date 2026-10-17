# Taiga MCP Server — MCP-сервер для Taiga REST API

[English](README.en.md) | **Русский**

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![MCP Server](https://img.shields.io/badge/MCP%20Server-Active-green.svg)](https://modelcontextprotocol.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

## Описание проекта

**Taiga MCP Server** — сервер Model Context Protocol (MCP) на C#/.NET 8.0, который предоставляет LLM-агентам доступ к REST API **Taiga** по HTTP. Сервер предоставляет 72 инструмента для управления проектами, пользовательскими историями, задачами, проблемами, эпиками, ролями, пользователями и участниками проекта.

- **Транспорт:** Streamable HTTP (`ModelContextProtocol.AspNetCore`).
- **Платформа:** .NET 8.0.
- **Источники:** только официальный C# MCP SDK и REST API Taiga (v1).

## Ключевые функции

- **72 MCP-инструмента** — проекты, пользовательские истории, задачи, проблемы, эпики, роли, пользователи, участники/приглашения.
- **Авторизация Taiga** — вход по логину/паролю, готовый токен, refresh-токен, а также cookie-сессия (для SSO).
- **Оптимистичный контроль версий (OCC)** — параметр `version` при изменении объектов.
- **Управление пагинацией** — заголовок `x-disable-pagination`.
- **Устойчивость** — автоматический повтор при `408`/`429`/`5xx` и сетевых сбоях с экспоненциальной задержкой.
- **Корпоративные/самоподписанные сертификаты** — pinning сертификата или доверенный корневой ЦС.
- **Защита MCP-эндпоинта** — общий токен (`X-Api-Key`/`Bearer`), сравнение с постоянным временем.
- **Конфигурируемый адрес и порт** прослушивания (секция `Server`).
- **Health-эндпоинты** — `/health`, `/health/live`, `/health/ready` (readiness с кэшированием).
- **Аудит деструктивных инструментов** — ILogger + файл JSON Lines c ротацией.
- **Rate limiting** — ограничение частоты запросов к MCP-эндпоинту (ключ — токен или IP).
- **Метрики Prometheus** (`/metrics`) — RPS, latency и error rate вызовов инструментов.
- **Корректное завершение (graceful shutdown)** и остановка при критических ошибках.
- **Конфигурация через ENV** — параметры переопределяются переменными окружения.
- **Docker** — мультистейдж `Dockerfile`: приложение в `/opt/mcp`, запуск от non-root (`app`, UID 1654), параметры через ENV, конфиг и сертификаты через монтирование.
- **Unit-тесты** (xUnit) с покрытием security-критичных путей.

## Требования

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) или новее (проект нацелен на `net8.0`).
- Доступ к инстансу Taiga по REST API (`/api/v1`).

## Быстрый старт

```powershell
# Сборка
dotnet build .\TaigaMcp.csproj -c Release

# Запуск (по умолчанию слушает 0.0.0.0:5000)
dotnet run --project .\TaigaMcp.csproj

# Unit-тесты
dotnet test .\tests\TaigaMcp.Tests\TaigaMcp.Tests.csproj
```

Проверка работоспособности:

```powershell
curl http://127.0.0.1:5000/health         # -> ok (общая проверка)
curl http://127.0.0.1:5000/health/live    # -> ok (процесс жив)
curl http://127.0.0.1:5000/health/ready   # -> {"status":"Healthy"} (доступ к Taiga API)
```

MCP-эндпоинт — корень приложения: `http://127.0.0.1:5000/`.

## Архитектура

```
TaigaMcp.csproj              # Проект net8.0 + пакеты MCP SDK
Program.cs                   # Точка входа: конфигурация, DI, HTTP-транспорт, защита эндпоинта
appsettings.json             # Базовые настройки (без секретов)
appsettings.Local.json       # Локальные секреты (не коммитится; монтируется в Docker)
Configuration/
  TaigaOptions.cs            # Модель настроек подключения
  ServerOptions.cs           # Адрес и порт прослушивания
  AuditOptions.cs            # Настройки аудит-лога
  MetricsOptions.cs          # Настройки метрик Prometheus
  RateLimitOptions.cs        # Настройки ограничения частоты запросов
  ShutdownOptions.cs         # Настройки корректного завершения
  HealthOptions.cs           # Настройки health/readiness
Health/
  HealthChecks.cs            # Логика проверок готовности/живости
  HealthEndpoints.cs         # Регистрация /health, /health/live, /health/ready
  ReadinessProbe.cs          # Кэширующая проверка readiness (single-flight)
Hosting/
  GracefulShutdown.cs        # Корректное завершение и реакция на критические ошибки
Logging/
  AuditLogger.cs             # Аудит-лог деструктивных инструментов (+ ротация)
Metrics/
  McpMetrics.cs              # Метрики Prometheus (RPS, latency, error rate)
  ToolNameResolver.cs        # Определение имени инструмента по методу
Security/
  RateLimitSetup.cs          # Rate limiting (partition по токену/IP)
Taiga/
  TaigaClient.cs             # Обёртка REST API: авторизация, повторы, пагинация, форматирование
  TaigaAuthStore.cs          # Singleton-кэш токенов
  TaigaHttpHandlerFactory.cs # Обработка TLS (pinning / доверенный ЦС / отключение проверки)
  TaigaApiException.cs       # Ошибки Taiga API
  JsonBody.cs                # Построитель JSON-тел запросов
Tools/
  TaigaToolBase.cs           # Базовый класс инструментов (+ аудит, метрики)
  *Tools.cs                  # 8 наборов MCP-инструментов
certs/                       # Сертификаты (напр. для pinning)
tests/
  TaigaMcp.Tests/            # Unit-тесты (xUnit)
Dockerfile                   # Образ: /opt/mcp, запуск от non-root (app)
.dockerignore                # Что не попадает в контекст сборки (секреты, certs, tests)
global.json                  # Pin версии .NET SDK
```

## Инструменты (72)

### Projects (10)

`taiga_list_projects`, `taiga_get_project`, `taiga_get_project_by_slug`, `taiga_create_project`, `taiga_update_project`, `taiga_delete_project`, `taiga_get_project_stats`, `taiga_leave_project`, `taiga_like_project`, `taiga_unlike_project`

### User Stories (11)

`taiga_list_userstories`, `taiga_get_userstory`, `taiga_get_userstory_by_ref`, `taiga_create_userstory`, `taiga_update_userstory`, `taiga_delete_userstory`, `taiga_bulk_create_userstories`, `taiga_vote_userstory`,`taiga_unvote_userstory`, `taiga_watch_userstory`, `taiga_unwatch_userstory`

### Tasks (9)

`taiga_list_tasks`, `taiga_get_task`, `taiga_get_task_by_ref`, `taiga_create_task`, `taiga_update_task`, `taiga_delete_task`, `taiga_bulk_create_tasks`, `taiga_watch_task`, `taiga_unwatch_task`

### Issues (8)

`taiga_list_issues`, `taiga_get_issue`, `taiga_get_issue_by_ref`, `taiga_create_issue`, `taiga_update_issue`, `taiga_delete_issue`, `taiga_watch_issue`, `taiga_unwatch_issue`

### Epics (13)

`taiga_list_epics`, `taiga_get_epic`, `taiga_get_epic_by_ref`, `taiga_create_epic`, `taiga_update_epic`, `taiga_delete_epic`, `taiga_bulk_create_epics`, `taiga_list_epic_related_userstories`, `taiga_create_epic_related_userstory`, `taiga_delete_epic_related_userstory`, `taiga_vote_epic`, `taiga_watch_epic`, `taiga_unwatch_epic`

### Roles (5)

`taiga_list_roles`, `taiga_get_role`, `taiga_create_role`, `taiga_update_role`, `taiga_delete_role`

### Users (8)

`taiga_list_users`, `taiga_get_user`, `taiga_get_current_user`, `taiga_register_user`, `taiga_update_user`,`taiga_delete_user`, `taiga_list_user_contacts`, `taiga_request_password_recovery`

### Memberships / Invitations (8)

`taiga_list_memberships`, `taiga_get_membership`, `taiga_create_membership`, `taiga_bulk_create_memberships`, `taiga_update_membership`, `taiga_delete_membership`, `taiga_resend_invitation`, `taiga_get_invitation`

## Конфигурация

Настройки читаются в порядке возрастания приоритета:

1. `appsettings.json` — базовые значения (без секретов);
2. `appsettings.Local.json` — необязательный локальный файл с секретами (в `.gitignore`);
3. переменные окружения.

### Секция `Server`

| Ключ | По умолчанию | Описание |
|------|--------------|----------|
| `Host` | `0.0.0.0` | Адрес прослушивания: `0.0.0.0` — все интерфейсы, `127.0.0.1`/`localhost` — только локально, либо конкретный IP. |
| `Port` | `5000` | Порт прослушивания. |

### Секция `Taiga`

| Ключ | По умолчанию | Описание |
|------|--------------|----------|
| `BaseUrl` | `https://api.taiga.io/api/v1` | Базовый адрес API Taiga. |
| `Username` | — | Логин или email. |
| `Password` | — | Пароль. |
| `Token` | — | Готовый `auth_token` (вариант для SSO). |
| `RefreshToken` | — | Refresh-токен. |
| `AuthMethod` | `auto` | `auto` \| `password` \| `token` \| `cookie` (см. ниже). |
| `AuthScheme` | `Bearer` | Схема заголовка `Authorization` (`Bearer` или `Application`). |
| `Cookie` | — | Значение заголовка `Cookie` для режима `cookie`. |
| `CaCertificatePath` | — | Путь к сертификату (pinning/корневой ЦС). Относительный путь — от каталога сборки. |
| `IgnoreSslErrors` | `false` | Полностью отключить проверку TLS-сертификата (крайняя мера). |
| `DisablePaginationByDefault` | `true` | Отключать пагинацию (заголовок `x-disable-pagination`). |
| `TimeoutSeconds` | `30` | Таймаут HTTP-запросов к Taiga (сек). |
| `MaxRetries` | `3` | Число повторов при временных сбоях. |
| `RetryBaseDelayMs` | `500` | Базовая задержка повтора (мс), растёт экспоненциально. |

### Секция `Mcp`

| Ключ | Описание |
|------|----------|
| `AuthToken` | Общий токен для доступа к MCP-эндпоинту. Если пуст — эндпоинт не защищён (только для локальной работы). |

### Секция `Audit`

| Ключ | По умолчанию | Описание |
|------|--------------|----------|
| `Enabled` | `true` | Включён ли аудит-лог деструктивных инструментов. |
| `FilePath` | `logs/audit.log` | Файл аудит-лога (JSON Lines). Если пусто — запись только в ILogger (категория `TaigaMcp.Audit`). Относительный путь — от каталога сборки. |
| `MaxFileSizeMB` | `64` | Максимальный размер файла в МБ до ротации; `0` — ротация отключена. |
| `MaxBackupFiles` | `5` | Число архивных копий (`audit.log.1` … `audit.log.N`); самая старая удаляется. |

### Секция `Metrics`

| Ключ | По умолчанию | Описание |
|------|--------------|----------|
| `Enabled` | `true` | Включён ли сбор и экспорт метрик Prometheus. |
| `Path` | `/metrics` | Путь, по которому отдаются метрики. Эндпоинт не защищён токеном MCP. |

Экспортируемые метрики вызовов инструментов:

| Метрика | Тип | Назначение |
|---------|-----|------------|
| `taiga_mcp_tool_requests_total{tool,status}` | counter | RPS (всего вызовов инструментов). |
| `taiga_mcp_tool_errors_total{tool}` | counter | Error rate (число ошибок). |
| `taiga_mcp_tool_request_duration_seconds{tool}` | histogram | Latency (длительность вызова). |

`UseHttpMetrics` дополнительно отдаёт стандартные HTTP-метрики (`http_requests_received_total`, `http_request_duration_seconds`, ...).

### Секция `RateLimit`

| Ключ | По умолчанию | Описание |
|------|--------------|----------|
| `Enabled` | `true` | Включено ли ограничение частоты запросов. |
| `PermitLimit` | `120` | Максимум запросов за окно на ключ (токен или IP). |
| `WindowSeconds` | `60` | Длительность окна в секундах. |
| `QueueLimit` | `0` | Размер очереди ожидания; `0` — запросы сверх лимита сразу получают `429`. |

Ключ партиционирования — токен доступа (стабильный хеш) или IP клиента. Health- и metrics-эндпоинты под ограничение не попадают. При превышении возвращается `429 Too Many Requests` с заголовком `Retry-After`.

### Секция `Shutdown`

| Ключ | По умолчанию | Описание |
|------|--------------|----------|
| `ShutdownTimeoutSeconds` | `30` | Сколько секунд ждать завершения текущих запросов при остановке. |
| `ShutdownOnUnhandledException` | `true` | Инициировать корректную остановку при необработанном исключении. |

### Секция `Health`

| Ключ | По умолчанию | Описание |
|------|--------------|----------|
| `ReadyCacheSeconds` | `60` | Интервал кэширования результата `/health/ready` в секундах. Реальный запрос к Taiga выполняется не чаще одного раза за интервал; `0` — проверять при каждом запросе. |

### Переменные окружения

Переопределяют соответствующие настройки:

| Переменная | Аналог в конфиге |
|------------|------------------|
| `MCP_HOST` | `Server:Host` |
| `MCP_PORT` | `Server:Port` |
| `MCP_AUTH_TOKEN` | `Mcp:AuthToken` |
| `AUDIT_ENABLED` | `Audit:Enabled` |
| `AUDIT_FILE_PATH` | `Audit:FilePath` |
| `AUDIT_MAX_FILE_SIZE_MB` | `Audit:MaxFileSizeMB` |
| `AUDIT_MAX_BACKUP_FILES` | `Audit:MaxBackupFiles` |
| `HEALTH_READY_CACHE_SECONDS` | `Health:ReadyCacheSeconds` |
| `METRICS_ENABLED` | `Metrics:Enabled` |
| `METRICS_PATH` | `Metrics:Path` |
| `RATE_LIMIT_ENABLED` | `RateLimit:Enabled` |
| `RATE_LIMIT_PERMIT` | `RateLimit:PermitLimit` |
| `RATE_LIMIT_WINDOW_SECONDS` | `RateLimit:WindowSeconds` |
| `SHUTDOWN_TIMEOUT_SECONDS` | `Shutdown:ShutdownTimeoutSeconds` |
| `SHUTDOWN_ON_UNHANDLED_EXCEPTION` | `Shutdown:ShutdownOnUnhandledException` |
| `TAIGA_URL` | `Taiga:BaseUrl` |
| `TAIGA_USERNAME` | `Taiga:Username` |
| `TAIGA_PASSWORD` | `Taiga:Password` |
| `TAIGA_TOKEN` | `Taiga:Token` |
| `TAIGA_REFRESH_TOKEN` | `Taiga:RefreshToken` |
| `TAIGA_AUTH_METHOD` | `Taiga:AuthMethod` |
| `TAIGA_AUTH_SCHEME` | `Taiga:AuthScheme` |
| `TAIGA_COOKIE` | `Taiga:Cookie` |
| `TAIGA_CA_CERT_PATH` | `Taiga:CaCertificatePath` |
| `TAIGA_IGNORE_SSL_ERRORS` | `Taiga:IgnoreSslErrors` |
| `TAIGA_TIMEOUT_SECONDS` | `Taiga:TimeoutSeconds` |
| `TAIGA_MAX_RETRIES` | `Taiga:MaxRetries` |
| `TAIGA_RETRY_DELAY_MS` | `Taiga:RetryBaseDelayMs` |

### Пример `appsettings.Local.json`

```json
{
  "Mcp": {
    "AuthToken": "<сгенерируйте случайный токен>"
  },
  "Taiga": {
    "BaseUrl": "https://taiga.example.local/api/v1",
    "Username": "user",
    "Password": "password",
    "CaCertificatePath": "certs/taiga_leaf.pem",
    "IgnoreSslErrors": false
  }
}
```

## Авторизация в Taiga

`AuthMethod` определяет способ получения токена:

- `auto` (по умолчанию) — сначала заданный `Token`, затем `RefreshToken`, затем вход по `Username`/`Password`;
- `password` — всегда выполнять вход по логину/паролю;
- `token` — использовать только заданный `Token` (вход по API не выполняется; удобно при SSO — токен берётся из открытой сессии Taiga);
- `cookie` — отправлять заголовок `Cookie` (сессия SSO/прокси), `Authorization` не используется.

Токен кэшируется в одном месте (singleton) и переиспользуется между вызовами; при `401` выполняется
одно автоматическое обновление (refresh или повторный вход).

## Корпоративный / самоподписанный сертификат

Если серверу Taiga не доверяет ОС, используйте один из вариантов:

1. **Pinning сертификата** (предпочтительно): положите сертификат сервера (PEM/DER/PFX) и укажите `CaCertificatePath`.
   Проверка проходит при точном совпадении отпечатка, а если это корневой ЦС — по цепочке.
2. **Отключение проверки**: `IgnoreSslErrors=true`. Небезопасно (риск MITM), только для доверенной сети.

Новую версию сертификата нужно подкладывать при ротации.

## Подключение MCP-клиента

Пример конфигурации (точная схема зависит от клиента; транспорт — Streamable HTTP):

```json
{
  "mcpServers": {
    "taiga": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:5000/",
      "headers": { "X-Api-Key": "<MCP_AUTH_TOKEN>" }
    }
  }
}
```

Если `Mcp:AuthToken` задан, передавайте его в заголовке `X-Api-Key` (или `Authorization: Bearer <token>`).
Без токена запросы к MCP получают `401`. Health-эндпоинты (`/health*`) всегда открыты.

## Health-эндпоинты

| Эндпоинт | Назначение | Ответ |
|----------|------------|-------|
| `/health` | Общая проверка (совместимость). | `ok` |
| `/health/live` | Liveness: процесс жив и отвечает. | `ok` |
| `/health/ready` | Readiness: настроен доступ к Taiga API. Результат кэшируется (`Health:ReadyCacheSeconds`, по умолчанию 60 с), реальный запрос `users/me` выполняется не чаще этого интервала. | `{"status":"Healthy"}` (200) или `{"status":"Unhealthy","reason":"..."}` (503) |

Все health-эндпоинты не защищены токеном MCP и не относятся к MCP.

## Метрики и ограничение частоты

- `GET /metrics` отдаёт метрики в формате Prometheus (значение `Metrics:Path`). Эндпоинт открыт (как и health) и не требует токена; при необходимости ограничьте к нему сетевой доступ.
- Запросы к MCP-эндпоинту ограничиваются политикой `RateLimit` (см. секцию выше): ключ — токен (`X-Api-Key`/`Bearer`) или IP клиента. При превышении возвращается `429` и заголовок `Retry-After`.

## Завершение работы

- По `SIGTERM`/`Ctrl+C` сервер корректно завершается, дожидаясь текущих запросов не дольше `Shutdown:ShutdownTimeoutSeconds` (по умолчанию 30 сек).
- При необработанном исключении (если `Shutdown:ShutdownOnUnhandledException=true`) в лог пишется `Critical` и инициируется корректная остановка.

## Docker

Образ устанавливает приложение в `/opt/mcp` и запускает его от непривилегированного пользователя `app` (UID 1654), а не от root. Параметры запуска задаются переменными окружения; конфигурация и сертификаты монтируются как файлы.

Сборка:

```bash
docker build -t taiga-mcp:latest .
```

Запуск — параметры через ENV/`--env-file`, конфиг и сертификат через монтирование:

```bash
docker run -d --name taiga-mcp \
  -p 8080:8080 \
  --env-file .env \
  -v "$PWD/appsettings.Local.json:/opt/mcp/appsettings.Local.json:ro" \
  -v "$PWD/certs/taiga_leaf.pem:/opt/mcp/certs/taiga_leaf.pem:ro" \
  taiga-mcp:latest
```

Минимальный `.env` (не коммитится):

```dotenv
MCP_AUTH_TOKEN=...
TAIGA_URL=https://taiga.example.local/api/v1
TAIGA_USERNAME=user
TAIGA_PASSWORD=...
TAIGA_CA_CERT_PATH=/opt/mcp/certs/taiga_leaf.pem
```

Примечания:

- `MCP_HOST`/`MCP_PORT` внутри образа по умолчанию `0.0.0.0`/`8080`; внешний порт публикуется через `-p`.
- Каталог аудит-лога `/opt/mcp/logs` принадлежит пользователю `app`; при необходимости вынесите его на том: `-v taiga-logs:/opt/mcp/logs`.
- Сертификат в образ не встраивается — монтируется при запуске. Если корпоративный сертификат не нужен, очистите `TAIGA_CA_CERT_PATH`.
- Секреты в образ не попадают: `.dockerignore` исключает `appsettings.Local.json`, `.env` и `certs/`.

## Безопасность

- Секреты храните в `appsettings.Local.json` (в `.gitignore`) или в переменных окружения / secret-manager. Не коммитьте их.
- По умолчанию сервер слушает `0.0.0.0` (все интерфейсы). Обязательно задайте `Mcp:AuthToken`, если сервер доступен из сети; для локальной работы ограничьте адрес до `127.0.0.1`.
- Предпочитайте pinning сертификата вместо `IgnoreSslErrors`.
- Деструктивные инструменты (`taiga_delete_*`, `taiga_delete_epic_related_userstory`) записываются в аудит-лог (`Audit:FilePath`).
- Ограничивайте частоту запросов к MCP-эндпоинту (секция `RateLimit`), чтобы защититься от перегрузки и подбора токена.
- Эндпоинты `/health*` и `/metrics` открыты (без токена MCP) — при публикации сервера в сеть закройте их на уровне прокси/файрвола.
- Учитывайте, что инструменты могут выполнять необратимые действия (`taiga_delete_*`, `taiga_register_user`,
  `taiga_request_password_recovery`); выдавайте сервисной учётной записи Taiga минимально необходимые права.

## Ограничения

- Режим cookie-авторизации SSO не добавляет заголовок `X-CSRFToken` — для некоторых конфигураций может потребоваться доработка.
- `/health/ready` не опрашивает Taiga при каждом запросе: результат кэшируется (`Health:ReadyCacheSeconds`, по умолчанию 60 с), поэтому частый polling не создаёт лишней нагрузки.
- Rate limiting хранится в памяти процесса; при запуске нескольких экземпляров лимит действует отдельно для каждого.
- CI (сборка пайплайнов) в проект не включён; unit-тесты запускаются вручную.

## Тестирование

Проект `tests/TaigaMcp.Tests` покрывает ключевые пути регрессионными тестами (xUnit, 75 тестов):

- `ToolsTests` / `ToolsInvocationTests` — регистрация и вызовы всех 72 инструментов;
- `TaigaClientTests` — авторизация (в т.ч. устойчивость к некорректному JSON), повторы, пагинация;
- `TaigaHttpHandlerFactoryTests` — ранняя валидация и обработка сертификата (pinning);
- `TaigaAuthStoreTests` — потокобезопасность кэша токенов;
- `AuditLoggerTests` — запись и ротация аудит-лога;
- `ReadinessProbeTests` — кэширование readiness (`ReadyCacheSeconds`);
- `RateLimitSetupTests`, `MetricsTests`, `GracefulShutdownTests`, `HealthChecksTests`, `OptionsDefaultsTests`.

```bash
dotnet test .\tests\TaigaMcp.Tests\TaigaMcp.Tests.csproj -c Release
```

Сбор покрытия (coverlet):

```bash
dotnet test .\tests\TaigaMcp.Tests\TaigaMcp.Tests.csproj -c Release \
  --collect:"XPlat Code Coverage" --results-directory .\TestResults
```

## Технологии

- [.NET 8.0](https://dotnet.microsoft.com/)
- [Model Context Protocol (MCP)](https://modelcontextprotocol.io/)
- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol)
- [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore)
- [prometheus-net.AspNetCore](https://www.nuget.org/packages/prometheus-net.AspNetCore)
- [Microsoft.Extensions.Logging](https://learn.microsoft.com/dotnet/core/extensions/logging)
- [System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/)

## Ссылки

- [Model Context Protocol](https://modelcontextprotocol.io/) — официальный сайт MCP
- [Taiga REST API](https://docs.taiga.io/) — документация API Taiga
- [.NET Documentation](https://learn.microsoft.com/dotnet/) — документация .NET

## Лицензия

Проект распространяется под лицензией **MIT** — см. файл [LICENSE.txt](LICENSE.txt).

---

**Последнее обновление:** 2026
