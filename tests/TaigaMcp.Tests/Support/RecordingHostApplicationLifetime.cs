using Microsoft.Extensions.Hosting;

namespace TaigaMcp.Tests.Support;

/// <summary>
/// Реализация <see cref="IHostApplicationLifetime"/> с управляемыми токенами,
/// позволяющая инициировать события Stopping/Stopped в тестах.
/// </summary>
internal sealed class RecordingHostApplicationLifetime : IHostApplicationLifetime
{
    private readonly CancellationToken _stopping;

    public RecordingHostApplicationLifetime(CancellationToken stopping) => _stopping = stopping;

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => _stopping;

    public CancellationToken ApplicationStopped => _stopping;

    public bool StopRequested { get; private set; }

    public void StopApplication() => StopRequested = true;
}
