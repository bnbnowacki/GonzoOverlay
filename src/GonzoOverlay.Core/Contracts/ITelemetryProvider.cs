using GonzoOverlay.Core.Models;

namespace GonzoOverlay.Core.Contracts;

public interface ITelemetryProvider : IAsyncDisposable
{
    event Action<TelemetrySnapshot>? TelemetryUpdated;

    event Action<WeatherSnapshot>? WeatherUpdated;

    event Action<StandingsSnapshot>? StandingsUpdated;

    event Action<string>? ConnectionStateChanged;

    event Action<Exception>? Error;

    Task StartAsync(CancellationToken cancellationToken);
}
