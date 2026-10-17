using Microsoft.Extensions.Hosting;

namespace TaigaMcp.Tests.Support;

/// <summary>Простая реализация <see cref="IHostApplicationLifetime"/> для проверки остановки.</summary>
internal sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public bool StopRequested { get; private set; }

    public void StopApplication() => StopRequested = true;
}
