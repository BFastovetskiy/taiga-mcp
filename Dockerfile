# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Образ MCP-сервера Taiga.
#
#  - приложение устанавливается в /opt/mcp;
#  - процесс запускается от непривилегированного пользователя (app, UID 1654);
#  - параметры запуска задаются переменными окружения (ENV / --env-file);
#  - конфигурацию (appsettings.Local.json) и сертификаты монтируют как файлы
#    при запуске, а не встраивают в образ (см. README.md, раздел "Docker").
# ---------------------------------------------------------------------------

# ------------------------------ Сборка -------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Сначала только манифесты — так слой restore кэшируется между сборками.
COPY global.json ./
COPY TaigaMcp.csproj ./
RUN dotnet restore TaigaMcp.csproj

# Затем исходный код и публикация в /opt/mcp внутри промежуточного образа.
COPY . .
RUN dotnet publish TaigaMcp.csproj \
        -c Release \
        -o /opt/mcp \
        --no-restore \
        /p:UseAppHost=false

# ------------------------------ Запуск -------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Приложение располагается строго в /opt/mcp.
WORKDIR /opt/mcp

# Значения по умолчанию. Любое из них можно переопределить через ENV / --env-file
# или через смонтированный appsettings.Local.json.
ENV DOTNET_EnableDiagnostics=0 \
    MCP_HOST=0.0.0.0 \
    MCP_PORT=8080 \
    AUDIT_FILE_PATH=/opt/mcp/logs/audit.log \
    TAIGA_CA_CERT_PATH=/opt/mcp/certs/taiga_leaf.pem

COPY --from=build /opt/mcp /opt/mcp

# Каталоги под аудит-лог и сертификаты (точки монтирования). curl — для HEALTHCHECK.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /opt/mcp/logs /opt/mcp/certs \
 && chown -R app:app /opt/mcp

# Запуск не от root: пользователь app (UID 1654) уже есть в образах .NET 8.
USER $APP_UID

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS "http://127.0.0.1:${MCP_PORT}/health/live" || exit 1

ENTRYPOINT ["dotnet", "/opt/mcp/TaigaMcp.dll"]
