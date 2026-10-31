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
    TelemetryVar.PitSvFuel,
    TelemetryVar.PitSvFlags,
    TelemetryVar.SessionLapsTotal,
    TelemetryVar.SessionUniqueID,
    TelemetryVar.Lap,
    TelemetryVar.SessionTimeRemain,
    TelemetryVar.SessionTimeTotal,
    TelemetryVar.SessionState,
    TelemetryVar.SessionFlags,
    TelemetryVar.OnPitRoad,
    TelemetryVar.PlayerTrackSurface,
    TelemetryVar.TrackWetness,
    TelemetryVar.CarIdxPosition,
    TelemetryVar.CarIdxF2Time,
    TelemetryVar.CarIdxEstTime,
    TelemetryVar.CarIdxLapDistPct,
    TelemetryVar.CarIdxOnPitRoad,
    TelemetryVar.PlayerCarIdx,
    TelemetryVar.CarIdxLastLapTime,
    TelemetryVar.CarIdxBestLapTime,
    TelemetryVar.CarIdxLap,
    TelemetryVar.CarIdxLapCompleted,
    TelemetryVar.SessionNum])]
internal static class IracingTelemetrySchema
{
}

public sealed class IracingTelemetryProvider : ITelemetryProvider
{
    private readonly ILogger<IracingTelemetryProvider> _logger;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly LeaderLapEstimator _leaderLapEstimator = new();
    private readonly FuelLapTracker _fuelLapTracker = new();
    private readonly StandingsLapGap _standingsLapGap = new();
    private (int? Session, int? SessionId, int? CarIdx)? _fuelLogContext;
    private int? _fuelLogPreviousLap;
    private float? _pitSpeedLimitMetersPerSecond;
    private IReadOnlyDictionary<int, DriverMetadata> _drivers =
        new Dictionary<int, DriverMetadata>();
    private IReadOnlyDictionary<int, string> _sessionTypes =
        new Dictionary<int, string>();
    private IReadOnlyList<double?>? _previousCarIdxLap;
    private IReadOnlyList<double?>? _previousCarIdxLapCompleted;
    private IReadOnlyList<double?>? _previousCarIdxLastLapTime;
    private IReadOnlyList<double?>? _previousCarIdxBestLapTime;

    public IracingTelemetryProvider(ILogger<IracingTelemetryProvider> logger)
    {
        _logger = logger;
    }

    public event Action<TelemetrySnapshot>? TelemetryUpdated;

    public event Action<WeatherSnapshot>? WeatherUpdated;

    public event Action<StandingsSnapshot>? StandingsUpdated;

    public event Action<RelativeSnapshot>? RelativeUpdated;

    public event Action<FuelSnapshot>? FuelUpdated;

    public event Action<string>? DebugLogUpdated;

    public event Action<string>? FuelLogUpdated;

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
                        PublishDebugLapUpdates(data, snapshot.Timestamp);
                        PublishStandings(BuildStandingsSnapshot(data, snapshot.Timestamp));
                        var relative = BuildRelativeSnapshot(data, snapshot.Timestamp);
                        PublishRelative(relative);
                        var fuel = BuildFuelSnapshot(data, relative, snapshot.Timestamp);
                        PublishFuel(fuel);
                        PublishFuelLapLog(data, relative, fuel);
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
                        if (!state.ToString().Equals("Connected", StringComparison.OrdinalIgnoreCase))
                        {
                            _leaderLapEstimator.Reset();
                            _fuelLapTracker.Reset();
                            _standingsLapGap.Reset();
                            _fuelLogContext = null;
                            _fuelLogPreviousLap = null;
                        }
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

    private void PublishRelative(RelativeSnapshot snapshot)
    {
        try
        {
            RelativeUpdated?.Invoke(snapshot);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Błąd odbiorcy relative");
            PublishError(exception);
        }
    }

    private void PublishFuel(FuelSnapshot snapshot)
    {
        try
        {
            FuelUpdated?.Invoke(snapshot);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Błąd odbiorcy danych paliwa");
            PublishError(exception);
        }
    }

    private FuelSnapshot BuildFuelSnapshot(
        TelemetryData data,
        RelativeSnapshot relative,
        DateTimeOffset timestamp)
    {
        var sessionNumber = data.SessionNum ?? 0;
        var sessionType = _sessionTypes.TryGetValue(sessionNumber, out var value)
            ? value
            : "Unknown";
        var isRace = sessionType.Equals("Race", StringComparison.OrdinalIgnoreCase);
        var currentFuel = ToFloat(ToDouble(data.FuelLevel));
        var playerCarIdx = ReadInt(data.PlayerCarIdx) ?? -1;
        var completedLap = ReadInt(ReadArrayValue(
            ReadNumericArray(data.CarIdxLapCompleted),
            playerCarIdx));

        _fuelLapTracker.Update(
            ReadInt(data.SessionNum),
            ReadInt(data.SessionUniqueID),
            playerCarIdx >= 0 ? playerCarIdx : null,
            completedLap,
            currentFuel,
            data.OnPitRoad,
            isRace && !relative.RaceFinished,
            relative.SessionLapsTotal,
            relative.EstimatedRaceLapsTotal,
            ReadInt(data.PitSvFlags),
            ToFloat(ToDouble(data.PitSvFuel)));

        return new FuelSnapshot(
            sessionType,
            currentFuel,
            _fuelLapTracker.AverageConsumption,
            _fuelLapTracker.LapsOfFuelRemaining,
            _fuelLapTracker.FuelToAdd,
            _fuelLapTracker.FuelAtFinish,
            timestamp);
    }

    private void PublishFuelLapLog(TelemetryData data, RelativeSnapshot relative, FuelSnapshot fuel)
    {
        var context = (ReadInt(data.SessionNum), ReadInt(data.SessionUniqueID), ReadInt(data.PlayerCarIdx));
        if (_fuelLogContext != context)
        {
            _fuelLogContext = context;
            _fuelLogPreviousLap = null;
        }

        var carIdx = context.Item3 ?? -1;
        var completedLap = ReadInt(ReadArrayValue(ReadNumericArray(data.CarIdxLapCompleted), carIdx));
        if (!completedLap.HasValue || completedLap.Value < 0)
        {
            return;
        }

        var previousLap = _fuelLogPreviousLap;
        _fuelLogPreviousLap = completedLap;
        if (!previousLap.HasValue || completedLap.Value <= previousLap.Value)
        {
            return;
        }

        var flags = ReadInt(data.PitSvFlags);
        var plannedFuel = ToDouble(data.PitSvFuel);
        var fuelSelected = flags.HasValue ? (flags.Value & 0x10) != 0 ? "TAK" : "NIE" : "brak";
        var predictedLaps = relative.SessionLapsTotal is > 0 and < 32767
            ? relative.SessionLapsTotal
            : relative.EstimatedRaceLapsTotal is > 0 && double.IsFinite(relative.EstimatedRaceLapsTotal.Value)
                ? (int?)((int)Math.Floor(relative.EstimatedRaceLapsTotal.Value) + 1)
                : null;
        static string Number(double? value) => value.HasValue && double.IsFinite(value.Value)
            ? value.Value.ToString("0.000", CultureInfo.InvariantCulture)
            : "brak";

        var line = $"{fuel.Timestamp.ToLocalTime():HH:mm:ss.fff} | sesja={fuel.SessionType} ({context.Item1}/{context.Item2})" +
            $" | auto={carIdx} | ukończone={completedLap} | aktualny_lap={relative.CurrentLap?.ToString() ?? "brak"}" +
            $" | paliwo={Number(fuel.CurrentFuelLiters)} L | średnie={Number(fuel.AverageConsumptionPerLapLiters)} L/lap" +
            $" | paliwo_baza={Number(_fuelLapTracker.StrategyFuelAtLine)} L | dolane_od_mety={Number(_fuelLapTracker.FuelAddedSinceLine)} L" +
            $" | próbki_spalania={_fuelLapTracker.CleanSampleCount}/5 | zbieranie_po_picie={_fuelLapTracker.RebuildingAfterPit}" +
            $" | zasięg={Number(fuel.LapsOfFuelRemaining)} lap | dystans_wyścigu={predictedLaps?.ToString() ?? "brak"} lap" +
            $" | prognoza_surowa={Number(relative.EstimatedRaceLapsTotal)} | czas_pozostały={Number(relative.SessionTimeRemainingSeconds)} s" +
            $" | pit_road={data.OnPitRoad?.ToString() ?? "brak"} | tankowanie_zaznaczone={fuelSelected}" +
            $" | pit_flags={flags?.ToString() ?? "brak"} | ustawiona_dolewka={Number(plannedFuel)} L" +
            $" | dolewka={Number(fuel.FuelToAddLiters)} L (po_lapie={_fuelLapTracker.FuelToAddSnapshotLap?.ToString() ?? "brak"})" +
            $" | na_mecie={Number(fuel.FuelAtFinishLiters)} L (po_lapie={_fuelLapTracker.FuelAtFinishSnapshotLap?.ToString() ?? "brak"})" +
            $" | meta={relative.RaceFinished}";

        try
        {
            FuelLogUpdated?.Invoke(line);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Błąd odbiorcy logu paliwa");
        }
    }

    private void UpdateSessionMetadata(object session)
    {
        _drivers = ReadDrivers(session);
        _sessionTypes = ReadSessionTypes(session);
        _previousCarIdxLap = null;
        _previousCarIdxLapCompleted = null;
        _previousCarIdxLastLapTime = null;
        _previousCarIdxBestLapTime = null;
    }

    private void PublishDebugLapUpdates(TelemetryData data, DateTimeOffset timestamp)
    {
        var carIdxLap = ReadNumericArray(data.CarIdxLap);
        var carIdxLapCompleted = ReadNumericArray(data.CarIdxLapCompleted);
        var carIdxLastLapTime = ReadNumericArray(data.CarIdxLastLapTime);
        var carIdxBestLapTime = ReadNumericArray(data.CarIdxBestLapTime);

        if (_previousCarIdxLap is null ||
            _previousCarIdxLapCompleted is null ||
            _previousCarIdxLastLapTime is null ||
            _previousCarIdxBestLapTime is null)
        {
            _previousCarIdxLap = carIdxLap;
            _previousCarIdxLapCompleted = carIdxLapCompleted;
            _previousCarIdxLastLapTime = carIdxLastLapTime;
            _previousCarIdxBestLapTime = carIdxBestLapTime;
            return;
        }

        var carIndexes = _drivers.Count > 0
            ? _drivers.Keys
            : Enumerable.Range(0, Math.Max(
                Math.Max(carIdxLap.Length, carIdxLapCompleted.Length),
                Math.Max(carIdxLastLapTime.Length, carIdxBestLapTime.Length)));
        var timestampText = timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

        foreach (var carIdx in carIndexes.OrderBy(index => index))
        {
            PublishDebugValueIfChanged(
                timestampText,
                "CarIdxLap",
                carIdx,
                ReadArrayValue(carIdxLap, carIdx),
                ReadArrayValue(_previousCarIdxLap, carIdx));
            PublishDebugValueIfChanged(
                timestampText,
                "CarIdxLapCompleted",
                carIdx,
                ReadArrayValue(carIdxLapCompleted, carIdx),
                ReadArrayValue(_previousCarIdxLapCompleted, carIdx));
            PublishDebugValueIfChanged(
                timestampText,
                "CarIdxLastLapTime",
                carIdx,
                ReadArrayValue(carIdxLastLapTime, carIdx),
                ReadArrayValue(_previousCarIdxLastLapTime, carIdx));
            PublishDebugValueIfChanged(
                timestampText,
                "CarIdxBestLapTime",
                carIdx,
                ReadArrayValue(carIdxBestLapTime, carIdx),
                ReadArrayValue(_previousCarIdxBestLapTime, carIdx));
        }

        _previousCarIdxLap = carIdxLap;
        _previousCarIdxLapCompleted = carIdxLapCompleted;
        _previousCarIdxLastLapTime = carIdxLastLapTime;
        _previousCarIdxBestLapTime = carIdxBestLapTime;
    }

    private void PublishDebugValueIfChanged(
        string timestamp,
        string variableName,
        int carIdx,
        double? current,
        double? previous)
    {
        if (!HasDebugValueChanged(current, previous))
        {
            return;
        }

        var valueText = current?.ToString("0.000", CultureInfo.InvariantCulture) ?? "null";
        try
        {
            DebugLogUpdated?.Invoke(
                $"{timestamp} | {variableName} | {carIdx} | {valueText}");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Błąd odbiorcy debug logu telemetrii");
        }
    }

    private static bool HasDebugValueChanged(double? current, double? previous)
    {
        if (current.HasValue != previous.HasValue)
        {
            return true;
        }

        return current.HasValue &&
            previous.HasValue &&
            Math.Abs(current.Value - previous.Value) > 0.0005;
    }

    private StandingsSnapshot BuildStandingsSnapshot(TelemetryData data, DateTimeOffset timestamp)
    {
        var positions = ReadNumericArray(data.CarIdxPosition);
        var intervalsToLeader = ReadNumericArray(data.CarIdxF2Time);
        var lastLaps = ReadNumericArray(data.CarIdxLastLapTime);
        var bestLaps = ReadNumericArray(data.CarIdxBestLapTime);
        var startedLaps = ReadNumericArray(data.CarIdxLap);
        var completedLaps = ReadNumericArray(data.CarIdxLapCompleted);
        var sessionNumber = data.SessionNum ?? 0;
        var sessionType = _sessionTypes.TryGetValue(sessionNumber, out var value)
            ? value
            : "Unknown";
        var isRace = sessionType.Equals("Race", StringComparison.OrdinalIgnoreCase);
        _standingsLapGap.BeginSession(ReadInt(data.SessionNum), ReadInt(data.SessionUniqueID), isRace);
        var leader = isRace
            ? _drivers.Values.FirstOrDefault(driver => ReadArrayValue(positions, driver.CarIdx) == 1)
            : null;
        var leaderCompleted = leader is null ? null : ReadArrayValue(completedLaps, leader.CarIdx);

        var orderedDrivers = _drivers.Values
            .Where(driver => ReadArrayValue(positions, driver.CarIdx) > 0)
            .OrderBy(driver => ReadArrayValue(positions, driver.CarIdx))
            .ToList();

        var entries = orderedDrivers
            .Select((driver, index) =>
            {
                var position = ReadArrayValue(positions, driver.CarIdx);
                var f2Time = ToFloat(ReadArrayValue(intervalsToLeader, driver.CarIdx));
                var bestLap = ToFloat(ReadArrayValue(bestLaps, driver.CarIdx));
                var ahead = index > 0 ? orderedDrivers[index - 1] : null;
                var lapGaps = isRace
                    ? _standingsLapGap.UpdateDriver(driver.CarIdx,
                        ReadArrayValue(completedLaps, driver.CarIdx), leaderCompleted,
                        ahead?.CarIdx,
                        ahead is null ? null : ReadArrayValue(completedLaps, ahead.CarIdx))
                    : new StandingsLapGaps(null, null);
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
                    ReadInt(ReadArrayValue(completedLaps, driver.CarIdx)),
                    ReadInt(ReadArrayValue(startedLaps, driver.CarIdx)),
                    null,
                    isRace ? f2Time : bestLap,
                    lapGaps.Leader,
                    lapGaps.Ahead,
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

    private RelativeSnapshot BuildRelativeSnapshot(TelemetryData data, DateTimeOffset timestamp)
    {
        var positions = ReadNumericArray(data.CarIdxPosition);
        var estimatedTimesToPlayer = ReadNumericArray(data.CarIdxEstTime);
        var lapDistances = ReadNumericArray(data.CarIdxLapDistPct);
        var onPitRoad = ReadBooleanArray(data.CarIdxOnPitRoad);
        var currentLap = ReadInt(data.Lap);
        var sessionLapsTotal = ReadInt(data.SessionLapsTotal);
        var sessionTimeRemainingSeconds = ToDouble(data.SessionTimeRemain);
        var sessionTimeTotalSeconds = ToDouble(data.SessionTimeTotal);
        var sessionNumber = ReadInt(data.SessionNum);
        var isRace = sessionNumber.HasValue &&
            _sessionTypes.TryGetValue(sessionNumber.Value, out var sessionType) &&
            sessionType.Equals("Race", StringComparison.OrdinalIgnoreCase);
        // SDK session states 5/6 are checkered/cooldown; flag bit 0 is checkered.
        var sessionState = ReadInt(data.SessionState);
        var sessionFlags = ReadInt(data.SessionFlags);
        var raceFinished = isRace &&
            (sessionState is 5 or 6 || (sessionFlags.GetValueOrDefault() & 1) != 0);
        var estimatedRaceLapsTotal = _leaderLapEstimator.Update(
            sessionNumber,
            ReadInt(data.SessionUniqueID),
            isRace && !(sessionLapsTotal is > 0 and < 32767),
            raceFinished,
            positions,
            ReadNumericArray(data.CarIdxLapCompleted),
            ReadNumericArray(data.CarIdxLastLapTime),
            onPitRoad,
            sessionTimeRemainingSeconds,
            timestamp);
        var playerCarIdx = ReadInt(data.PlayerCarIdx) ?? -1;
        var playerPosition = ReadPosition(positions, playerCarIdx);
        var playerLapDistance = ReadArrayValue(lapDistances, playerCarIdx);
        var playerEstimatedTime = ToFloat(ReadArrayValue(
            estimatedTimesToPlayer,
            playerCarIdx));
        var playerClassEstLapTime = GetPlayerClassEstLapTime(playerCarIdx);

        if (!playerEstimatedTime.HasValue || !playerLapDistance.HasValue)
        {
            return new RelativeSnapshot(
                playerPosition,
                Array.Empty<RelativeEntry>(),
                currentLap,
                sessionLapsTotal,
                estimatedRaceLapsTotal,
                _leaderLapEstimator.IsFinished,
                sessionTimeRemainingSeconds,
                sessionTimeTotalSeconds,
                timestamp);
        }

        var relativeEntries = _drivers.Values
            .Select(driver =>
            {
                var position = ReadPosition(positions, driver.CarIdx);
                var estimatedTime = ToFloat(ReadArrayValue(
                    estimatedTimesToPlayer,
                    driver.CarIdx));
                var driverLapDistance = ReadArrayValue(lapDistances, driver.CarIdx);
                var isOnPitRoad = ReadBooleanArrayValue(onPitRoad, driver.CarIdx) == true;
                var isPlayer = driver.CarIdx == playerCarIdx;
                var gap = isPlayer || !estimatedTime.HasValue ||
                    !playerEstimatedTime.HasValue || !driverLapDistance.HasValue
                    ? null
                    : CalculateRelativeGap(
                        playerEstimatedTime.Value,
                        estimatedTime.Value,
                        playerLapDistance.Value,
                        driverLapDistance.Value,
                        playerClassEstLapTime,
                        driver.ClassEstLapTime);

                return new RelativeEntry(
                    position,
                    driver.Driver,
                    gap,
                    isPlayer,
                    isOnPitRoad);
            })
            .Where(entry => entry.IsPlayer || entry.GapToPlayerSeconds.HasValue)
            .ToList();

        var playerEntry = relativeEntries.FirstOrDefault(entry => entry.IsPlayer);
        if (playerEntry is null)
        {
            return new RelativeSnapshot(
                playerPosition,
                Array.Empty<RelativeEntry>(),
                currentLap,
                sessionLapsTotal,
                estimatedRaceLapsTotal,
                _leaderLapEstimator.IsFinished,
                sessionTimeRemainingSeconds,
                sessionTimeTotalSeconds,
                timestamp);
        }

        var ahead = relativeEntries
            .Where(entry => entry.GapToPlayerSeconds > 0)
            .OrderBy(entry => entry.GapToPlayerSeconds)
            .Take(3)
            .ToList();
        var behind = relativeEntries
            .Where(entry => entry.GapToPlayerSeconds < 0)
            .OrderByDescending(entry => entry.GapToPlayerSeconds)
            .Take(3)
            .ToList();

        var entries = ahead
            .Append(playerEntry)
            .Concat(behind)
            .ToList();

        return new RelativeSnapshot(
            playerPosition,
            entries,
            currentLap,
            sessionLapsTotal,
            estimatedRaceLapsTotal,
            _leaderLapEstimator.IsFinished,
            sessionTimeRemainingSeconds,
            sessionTimeTotalSeconds,
            timestamp);
    }

    private float? GetPlayerClassEstLapTime(int playerCarIdx) =>
        _drivers.TryGetValue(playerCarIdx, out var player)
            ? player.ClassEstLapTime
            : null;

    private static float? CalculateRelativeGap(
        float playerEstimatedTime,
        float driverEstimatedTime,
        double playerLapDistance,
        double driverLapDistance,
        float? playerClassEstLapTime,
        float? driverClassEstLapTime)
    {
        if (playerLapDistance < 0 || playerLapDistance > 1 ||
            driverLapDistance < 0 || driverLapDistance > 1)
        {
            return null;
        }

        var referenceLapTime = playerClassEstLapTime.HasValue &&
            playerClassEstLapTime.Value > 0
            ? playerClassEstLapTime.Value
            : driverClassEstLapTime;
        var driverTimeOnPlayerScale = driverEstimatedTime;
        if (referenceLapTime.HasValue && referenceLapTime.Value > 0 &&
            driverClassEstLapTime.HasValue && driverClassEstLapTime.Value > 0)
        {
            driverTimeOnPlayerScale = driverEstimatedTime *
                referenceLapTime.Value / driverClassEstLapTime.Value;
        }

        var gap = driverTimeOnPlayerScale - playerEstimatedTime;
        var crossesStartFinish = Math.Abs(driverLapDistance - playerLapDistance) > 0.5;
        if (crossesStartFinish && referenceLapTime.HasValue && referenceLapTime.Value > 0)
        {
            gap += playerLapDistance > driverLapDistance
                ? referenceLapTime.Value
                : -referenceLapTime.Value;
        }

        return gap;
    }

    private static int ReadPosition(IReadOnlyList<double?> positions, int carIdx)
    {
        var position = ReadArrayValue(positions, carIdx);
        return position.HasValue && position.Value > 0
            ? (int)Math.Round(position.Value)
            : 0;
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
                ReadInt(ReadProperty(driver, "IRating")),
                ToFloat(ToDouble(ReadProperty(driver, "CarClassEstLapTime")))))
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

    private static bool?[] ReadBooleanArray(object? value) =>
        ReadItems(value).Select(item => ToBool(item)).ToArray();

    private static bool? ReadBooleanArrayValue(IReadOnlyList<bool?> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : null;

    private static int? ReadInt(object? value)
    {
        var number = ToDouble(value);
        return number.HasValue ? (int)number.Value : null;
    }

    private static float? ToFloat(double? value) => value.HasValue ? (float)value.Value : null;

    private static bool? ToBool(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is bool boolean)
        {
            return boolean;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToBoolean(CultureInfo.InvariantCulture);
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
        int? IRating,
        float? ClassEstLapTime);

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
