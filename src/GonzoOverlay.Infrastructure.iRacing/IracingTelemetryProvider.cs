using GonzoOverlay.Core.Contracts;
using GonzoOverlay.Core.Models;
using Microsoft.Extensions.Logging;
using SVappsLAB.iRacingTelemetrySDK;

namespace GonzoOverlay.Infrastructure.iRacing;

[RequiredTelemetryVars([
    TelemetryVar.Speed,
    TelemetryVar.RPM,
    TelemetryVar.Gear,
    TelemetryVar.FuelLevel,
    TelemetryVar.TrackWetness])]
internal static class IracingTelemetrySchema
{
}

public sealed class IracingTelemetryProvider : ITelemetryProvider
{
    private readonly ILogger<IracingTelemetryProvider> _logger;
    private readonly CancellationTokenSource _disposeCancellation = new();

    public IracingTelemetryProvider(ILogger<IracingTelemetryProvider> logger)
    {
        _logger = logger;
    }

    public event Action<TelemetrySnapshot>? TelemetryUpdated;

    public event Action<WeatherSnapshot>? WeatherUpdated;

    public event Action<string>? ConnectionStateChanged;

    public event Action<Exception>? Error;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCancellation.Token);
        var token = linkedCancellation.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                PublishConnectionState("Waiting for iRacing");

                await using var client = TelemetryClient<TelemetryData>.Create(_logger);
                var handlers = new TelemetryHandlers<TelemetryData>
                {
                    OnTelemetryUpdate = data =>
                    {
                        var speedMetersPerSecond = data.Speed;
                        var snapshot = new TelemetrySnapshot(
                            speedMetersPerSecond,
                            speedMetersPerSecond * 3.6f,
                            data.RPM,
                            data.Gear,
                            data.FuelLevel,
                            DateTimeOffset.UtcNow);

                        PublishTelemetry(snapshot);
                        PublishWeather(new WeatherSnapshot(
                            data.TrackWetness?.ToString() ?? "Unknown",
                            snapshot.Timestamp));
                        return Task.CompletedTask;
                    },
                    OnConnectStateChanged = state =>
                    {
                        PublishConnectionState(state.ToString());
                        return Task.CompletedTask;
                    },
                    OnError = error =>
                    {
                        _logger.LogError(error, "Błąd SDK telemetrii iRacing");
                        PublishError(error);
                        return Task.CompletedTask;
                    }
                };

                await client.Monitor(handlers, token);

                if (!token.IsCancellationRequested)
                {
                    PublishConnectionState("Monitor stopped; retrying");
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Monitor iRacing zakończył się błędem; ponawiam połączenie");
                PublishError(exception);
                PublishConnectionState("Retrying after error");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _disposeCancellation.Cancel();
        _disposeCancellation.Dispose();
        return ValueTask.CompletedTask;
    }

    private void PublishTelemetry(TelemetrySnapshot snapshot)
    {
        try
        {
            TelemetryUpdated?.Invoke(snapshot);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Błąd odbiorcy danych telemetrii");
            PublishError(exception);
        }
    }

    private void PublishWeather(WeatherSnapshot snapshot)
    {
        try
        {
            WeatherUpdated?.Invoke(snapshot);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Błąd odbiorcy stanu pogody");
            PublishError(exception);
        }
    }

    private void PublishConnectionState(string state)
    {
        try
        {
            ConnectionStateChanged?.Invoke(state);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Błąd odbiorcy stanu połączenia");
        }
    }

    private void PublishError(Exception exception)
    {
        try
        {
            Error?.Invoke(exception);
        }
        catch (Exception callbackException)
        {
            _logger.LogError(callbackException, "Błąd odbiorcy błędu telemetrii");
        }
    }
}
