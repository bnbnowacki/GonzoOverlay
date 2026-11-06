using System.Collections;
using System.Globalization;
using System.Reflection;
using GonzoOverlay.Core.Models;

namespace GonzoOverlay.Infrastructure.iRacing;

// Standings are built from the current SDK session snapshot, not telemetry history.
internal static class SessionStandingsBuilder
{
    public static StandingsSnapshot Build(object sessionData, DateTimeOffset timestamp, SessionRaceIntervalTracker? intervalTracker = null)
    {
        var info = Get(sessionData, "SessionInfo");
        var currentNumber = Int(Get(info, "CurrentSessionNum"));
        var currentSession = Items(Get(info, "Sessions"))
            .FirstOrDefault(item => Int(Get(item, "SessionNum")) == currentNumber && currentNumber.HasValue);
        var sessionType = Text(Get(currentSession, "SessionType")) ?? "Unknown";
        var isRace = sessionType.Equals("Race", StringComparison.OrdinalIgnoreCase);
        var driverInfo = Get(sessionData, "DriverInfo");
        var playerIdx = Int(Get(driverInfo, "DriverCarIdx"));
        var drivers = Items(Get(driverInfo, "Drivers"))
            .Where(driver => Int(Get(driver, "CarIdx")) is >= 0 &&
                Int(Get(driver, "CarIsPaceCar")) != 1 && Int(Get(driver, "IsSpectator")) != 1)
            .GroupBy(driver => Int(Get(driver, "CarIdx"))!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var results = Items(Get(currentSession, "ResultsPositions")).ToList();
        var qualifying = Items(Get(Get(sessionData, "QualifyResultsInfo"), "Results")).ToList();
        if (qualifying.Count == 0 && isRace)
        {
            var precedingQualifying = Items(Get(info, "Sessions"))
                .Where(item => (Text(Get(item, "SessionType")) ?? "").Contains("Qualify", StringComparison.OrdinalIgnoreCase) &&
                    Int(Get(item, "SessionNum")) < currentNumber)
                .OrderByDescending(item => Int(Get(item, "SessionNum")))
                .FirstOrDefault(item => Items(Get(item, "ResultsPositions")).Any());
            qualifying = Items(Get(precedingQualifying, "ResultsPositions")).ToList();
        }
        var firstRaceLap = Int(Get(currentSession, "ResultsLapsComplete")) is >= 1 ||
            results.Any(result => Int(Get(result, "LapsComplete")) is >= 1);
        var useQualifying = qualifying.Count > 0 &&
            ((isRace && !firstRaceLap) ||
             (!isRace && results.Count == 0 && sessionType.Contains("Qualify", StringComparison.OrdinalIgnoreCase)));
        var source = useQualifying ? qualifying : results;

        // QualifyResultsInfo uses zero-based positions; ResultsPositions may be zero- or one-based.
        var zeroBased = useQualifying && ReferenceEquals(source, qualifying) &&
            Items(Get(Get(sessionData, "QualifyResultsInfo"), "Results")).Any();
        zeroBased |= source.Any(item => Int(Get(item, "Position")) == 0);
        var rows = source.Select(result =>
        {
            var carIdx = Int(Get(result, "CarIdx"));
            var rawPosition = Int(Get(result, "Position"));
            if (!carIdx.HasValue || !rawPosition.HasValue || rawPosition < 0 ||
                !drivers.TryGetValue(carIdx.Value, out var driver)) return null;
            var position = rawPosition.Value + (zeroBased ? 1 : 0);
            if (position <= 0) return null;
            var best = LapTime(Get(result, "FastestTime"));
            var lapsBehind = isRace && !useQualifying ? NonNegativeInt(Get(result, "Lap")) : null;
            var entry = new StandingEntry(position, Text(Get(driver, "CarNumber")) ?? "",
                Text(Get(driver, "UserName")) ?? "Unknown",
                Text(Get(driver, "CarScreenNameShort")) ?? Text(Get(driver, "CarScreenName")) ?? "Unknown",
                useQualifying ? null : LapTime(Get(result, "LastTime")), best,
                useQualifying ? null : NonNegativeInt(Get(result, "LapsComplete")), null,
                null, isRace && !useQualifying ? NonNegativeTime(Get(result, "Time")) : null,
                lapsBehind, null, Text(Get(driver, "LicString")) ?? "",
                NonNegativeInt(Get(driver, "IRating")), playerIdx == carIdx);
            return new { CarIdx = carIdx.Value, Entry = entry };
        }).Where(entry => entry is not null).Select(entry => entry!)
            .OrderBy(row => row.Entry.Position).ToList();
        var entries = rows.Select(row => row.Entry).ToList();

        if (!isRace || useQualifying)
        {
            var best = entries.Where(entry => entry.BestLapSeconds.HasValue)
                .Select(entry => entry.BestLapSeconds!.Value).DefaultIfEmpty().Min();
            entries = entries.Select(entry => entry with
            {
                LeaderGapSeconds = best > 0 && entry.BestLapSeconds.HasValue
                    ? Math.Max(0, entry.BestLapSeconds.Value - best) : null
            }).ToList();
        }

        IReadOnlyDictionary<int, SessionInterval>? raceIntervals = null;
        if (isRace && !useQualifying)
        {
            var weekend = Get(sessionData, "WeekendInfo");
            var context = $"{Get(weekend, "SessionID")}/{Get(weekend, "SubSessionID")}/{currentNumber}/{sessionType}";
            raceIntervals = (intervalTracker ?? new SessionRaceIntervalTracker()).Update(context,
                rows.Select((row, index) => new SessionIntervalInput(row.CarIdx,
                    entries[index].LapsCompleted, entries[index].LeaderGapSeconds)).ToList());
        }
        else intervalTracker?.Reset();

        var withIntervals = entries.Select((entry, index) =>
        {
            if (index == 0) return entry;
            var previous = entries[index - 1];
            if (raceIntervals is not null)
            {
                var interval = raceIntervals.GetValueOrDefault(rows[index].CarIdx);
                return entry with { LapsBehindAhead = interval.Laps, IntervalSeconds = interval.Seconds };
            }
            var difference = entry.LeaderGapSeconds - previous.LeaderGapSeconds;
            return entry with
            {
                IntervalSeconds = difference >= 0 ? difference : null
            };
        }).ToList();
        return new StandingsSnapshot(sessionType, useQualifying, withIntervals, timestamp);
    }

    private static object? Get(object? item, string name) =>
        item?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(item);
    private static IEnumerable<object> Items(object? value) => value is IEnumerable items && value is not string
        ? items.Cast<object>() : Array.Empty<object>();
    private static string? Text(object? value) => value?.ToString();
    private static double? Number(object? value)
    {
        if (value is not IConvertible convertible) return null;
        try { var number = convertible.ToDouble(CultureInfo.InvariantCulture); return double.IsFinite(number) ? number : null; }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException) { return null; }
    }
    private static int? Int(object? value) => Number(value) is double number && number >= int.MinValue &&
        number <= int.MaxValue && Math.Truncate(number) == number ? (int)number : null;
    private static int? NonNegativeInt(object? value) => Int(value) is int number && number >= 0 ? number : null;
    private static float? NonNegativeTime(object? value) => Number(value) is double number && number >= 0 && number <= float.MaxValue
        ? (float)number : null;
    private static float? LapTime(object? value) => NonNegativeTime(value) is float number && number > 0 ? number : null;
}
