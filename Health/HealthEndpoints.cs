namespace TaigaMcp.Health;

/// <summary>
/// Регистрация health-эндпоинтов (не являются частью MCP):
///  - /health      — общая проверка (совместимость);
///  - /health/live — liveness: процесс жив и отвечает;
///  - /health/ready— readiness: настроен и доступен доступ к Taiga API
///                   (результат кэшируется через <see cref="ReadinessProbe"/>).
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Text("ok"));

        app.MapGet("/health/live", () => Results.Text("ok"));

        app.MapGet("/health/ready", async (ReadinessProbe probe, CancellationToken cancellationToken) =>
        {
            var result = await probe.GetAsync(cancellationToken).ConfigureAwait(false);

            return result.IsReady
                ? Results.Json(new { status = "Healthy" })
                : Results.Json(
                    new { status = "Unhealthy", reason = result.Reason },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        return app;
    }
}
