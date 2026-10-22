using System.Collections;
using System.Globalization;
using System.Reflection;
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
    TelemetryVar.OnPitRoad,
    TelemetryVar.PlayerTrackSurface,
    TelemetryVar.TrackWetness,
    TelemetryVar.CarIdxPosition,
    TelemetryVar.CarIdxF2Time,
    TelemetryVar.CarIdxLastLapTime,
    TelemetryVar.CarIdxBestLapTime,
    TelemetryVar.SessionNum])]
internal static class IracingTelemetrySchema
{
}

public sealed class IracingTelemetryProvider : ITelemetryProvider
{
    private readonly ILogger<IracingTelemetryProvider> _logger;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private float? _pitSpeedLimitMetersPerSecond;
    private IReadOnlyDictionary<int, DriverMetadata> _drivers =
        new Dictionary<int, DriverMetadata>();
    private IReadOnlyDictionary<int, string> _sessionTypes =
        new Dictionary<int, string>();

    public IracingTelemetryProvider(ILogger<IracingTelemetryProvider> logger)
    {
        _logger = logger;
    }

    public event Action<TelemetrySnapshot>? TelemetryUpdated;

    public event Action<WeatherSnapshot>? WeatherUpdated;

    public event Action<StandingsSnapshot>? StandingsUpdated;

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
                            _pitSpeedLimitMetersPerSecond,
                            data.OnPitRoad,
                            data.PlayerTrackSurface == TrackLocation.AproachingPits,
                            data.RPM,
                            data.Gear,
                            data.FuelLevel,
                            DateTimeOffset.UtcNow);

                        PublishTelemetry(snapshot);
                        PublishWeather(new WeatherSnapshot(
                            data.TrackWetness?.ToString() ?? "Unknown",
                            snapshot.Timestamp));
                        PublishStandings(BuildStandingsSnapshot(data, snapshot.Timestamp));
                        return Task.CompletedTask;
                    },
                    OnSessionInfoUpdate = session =>
                    {
                        UpdateSessionMetadata(session);
                        _pitSpeedLimitMetersPerSecond = ParsePitSpeedLimit(
                            session.WeekendInfo?.TrackPitSpeedLimit?.ToString());
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

    private static float? ParsePitSpeedLimit(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var numericPart = rawValue.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        if (!float.TryParse(
                numericPart,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return null;
        }

        if (rawValue.Contains("m/s", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        // WeekendInfo normally reports TrackPitSpeedLimit in kph.
        return value / 3.6f;
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

    private void PublishStandings(StandingsSnapshot snapshot)
    {
        try
        {
            StandingsUpdated?.Invoke(snapshot);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Błąd odbiorcy standings");
            PublishError(exception);
        }
    }

    private void UpdateSessionMetadata(object session)
    {
        _drivers = ReadDrivers(session);
        _sessionTypes = ReadSessionTypes(session);
    }

    private StandingsSnapshot BuildStandingsSnapshot(TelemetryData data, DateTimeOffset timestamp)
    {
        var positions = ReadNumericArray(data.CarIdxPosition);
        var intervalsToLeader = ReadNumericArray(data.CarIdxF2Time);
        var lastLaps = ReadNumericArray(data.CarIdxLastLapTime);
        var bestLaps = ReadNumericArray(data.CarIdxBestLapTime);
        var sessionNumber = data.SessionNum ?? 0;
        var sessionType = _sessionTypes.TryGetValue(sessionNumber, out var value)
            ? value
            : "Unknown";
        var isRace = sessionType.Equals("Race", StringComparison.OrdinalIgnoreCase);

        var entries = _drivers.Values
            .Select(driver =>
            {
                var position = ReadArrayValue(positions, driver.CarIdx);
                var f2Time = ToFloat(ReadArrayValue(intervalsToLeader, driver.CarIdx));
                var bestLap = ToFloat(ReadArrayValue(bestLaps, driver.CarIdx));
                if (!IsValidLap(bestLap) && !isRace && IsValidLap(f2Time))
                {
                    // In Practice/Qualify iRacing reports the fastest lap through F2Time.
                    bestLap = f2Time;
                }

                return new StandingEntry(
                    position.HasValue && position.Value > 0
                        ? (int)Math.Round(position.Value)
                        : int.MaxValue,
                    driver.Number,
                    driver.Driver,
                    driver.Car,
                    ToFloat(ReadArrayValue(lastLaps, driver.CarIdx)),
                    bestLap,
                    null,
                    isRace ? f2Time : bestLap,
                    driver.SafetyRating,
                    driver.IRating);
            })
            .Where(entry => entry.Position != int.MaxValue)
            .OrderBy(entry => entry.Position)
            .ToList();

        var normalizedEntries = !isRace
            ? NormalizePracticeLeaderGaps(entries)
            : entries;

        var withIntervals = normalizedEntries
            .Select((entry, index) =>
            {
                var previousLeaderGap = index > 0
                    ? normalizedEntries[index - 1].LeaderGapSeconds
                    : null;
                float? interval = entry.LeaderGapSeconds.HasValue && previousLeaderGap.HasValue
                    ? Math.Max(0, entry.LeaderGapSeconds.Value - previousLeaderGap.Value)
                    : null;

                return entry with { IntervalSeconds = interval };
            })
            .ToList();

        return new StandingsSnapshot(sessionType, withIntervals, timestamp);
    }

    private static IReadOnlyList<StandingEntry> NormalizePracticeLeaderGaps(
        IReadOnlyList<StandingEntry> entries)
    {
        var fastestLap = entries
            .Where(entry => IsValidLap(entry.BestLapSeconds))
            .Select(entry => entry.BestLapSeconds!.Value)
            .DefaultIfEmpty()
            .Min();

        if (fastestLap <= 0)
        {
            return entries;
        }

        return entries
            .Select(entry => entry with
            {
                LeaderGapSeconds = IsValidLap(entry.BestLapSeconds)
                    ? Math.Max(0, entry.BestLapSeconds!.Value - fastestLap)
                    : null
            })
            .ToList();
    }

    private static bool IsValidLap(float? value) => value.HasValue && value.Value > 0;

    private static IReadOnlyDictionary<int, DriverMetadata> ReadDrivers(object session)
    {
        var driverInfo = ReadProperty(session, "DriverInfo");
        var drivers = ReadItems(ReadProperty(driverInfo, "Drivers"));

        return drivers
            .Select(driver => new DriverMetadata(
                ReadInt(ReadProperty(driver, "CarIdx")) ?? -1,
                ReadString(ReadProperty(driver, "CarNumber")) ?? "",
                ReadString(ReadProperty(driver, "UserName")) ?? "Unknown",
                ReadString(ReadProperty(driver, "CarScreenNameShort")) ??
                    ReadString(ReadProperty(driver, "CarScreenName")) ?? "Unknown",
                ReadString(ReadProperty(driver, "LicString")) ?? "",
                ReadInt(ReadProperty(driver, "IRating"))))
            .Where(driver => driver.CarIdx >= 0)
            .ToDictionary(driver => driver.CarIdx);
    }

    private static IReadOnlyDictionary<int, string> ReadSessionTypes(object session)
    {
        var sessionInfo = ReadProperty(session, "SessionInfo");
        var sessions = ReadItems(ReadProperty(sessionInfo, "Sessions"));

        return sessions
            .Select(item => new
            {
                Number = ReadInt(ReadProperty(item, "SessionNum")),
                Type = ReadString(ReadProperty(item, "SessionType"))
            })
            .Where(item => item.Number.HasValue && !string.IsNullOrWhiteSpace(item.Type))
            .ToDictionary(item => item.Number!.Value, item => item.Type!);
    }

    private static object? ReadProperty(object? instance, string name) =>
        instance?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);

    private static IEnumerable<object> ReadItems(object? value)
    {
        if (value is not IEnumerable enumerable || value is string)
        {
            return Array.Empty<object>();
        }

        return enumerable.Cast<object>();
    }

    private static double?[] ReadNumericArray(object? value) =>
        ReadItems(value).Select(item => ToDouble(item)).ToArray();

    private static double? ReadArrayValue(IReadOnlyList<double?> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : null;

    private static int? ReadInt(object? value)
    {
        var number = ToDouble(value);
        return number.HasValue ? (int)number.Value : null;
    }

    private static float? ToFloat(double? value) => value.HasValue ? (float)value.Value : null;

    private static double? ToDouble(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToDouble(CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (InvalidCastException)
            {
                return null;
            }
        }

        return null;
    }

    private static string? ReadString(object? value) => value?.ToString();

    private sealed record DriverMetadata(
        int CarIdx,
        string Number,
        string Driver,
        string Car,
        string SafetyRating,
        int? IRating);

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
