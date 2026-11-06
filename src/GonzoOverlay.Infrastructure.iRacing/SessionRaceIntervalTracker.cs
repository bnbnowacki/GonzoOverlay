namespace GonzoOverlay.Infrastructure.iRacing;

internal readonly record struct SessionIntervalInput(int CarIdx, int? CompletedLaps, float? Time);
internal readonly record struct SessionInterval(int? Laps, float? Seconds);

// Results only record a car's time at its own scoring line; compare adjacent cars at the right crossing.
internal sealed class SessionRaceIntervalTracker
{
    private readonly Dictionary<int, int?> _previousLaps = new();
    private readonly Dictionary<int, DriverInterval> _intervals = new();
    private string? _context;

    public IReadOnlyDictionary<int, SessionInterval> Update(string context, IReadOnlyList<SessionIntervalInput> cars)
    {
        if (_context != context)
        {
            Reset();
            _context = context;
        }

        var output = new Dictionary<int, SessionInterval>();
        for (var index = 1; index < cars.Count; index++)
        {
            var car = cars[index];
            var ahead = cars[index - 1];
            _previousLaps.TryGetValue(car.CarIdx, out var oldCarLaps);
            _previousLaps.TryGetValue(ahead.CarIdx, out var oldAheadLaps);
            _intervals.TryGetValue(car.CarIdx, out var state);
            var newPair = state is null || state.AheadCarIdx != ahead.CarIdx;
            var carCrossed = car.CompletedLaps.HasValue && oldCarLaps.HasValue && car.CompletedLaps > oldCarLaps;
            var aheadCrossed = ahead.CompletedLaps.HasValue && oldAheadLaps.HasValue && ahead.CompletedLaps > oldAheadLaps;
            var countWentBackwards = (car.CompletedLaps.HasValue && oldCarLaps.HasValue && car.CompletedLaps < oldCarLaps) ||
                (ahead.CompletedLaps.HasValue && oldAheadLaps.HasValue && ahead.CompletedLaps < oldAheadLaps);

            SessionInterval interval;
            if (!car.CompletedLaps.HasValue || !ahead.CompletedLaps.HasValue || countWentBackwards)
                interval = new(null, null);
            else if (newPair || carCrossed)
            {
                interval = AtCarCrossing(car, ahead, newPair && !carCrossed);
                if (carCrossed && !newPair && car.CompletedLaps == ahead.CompletedLaps &&
                    (!car.Time.HasValue || car.Time == state!.CarTime))
                    interval = state!.Interval.Seconds.HasValue ? state.Interval : new(null, null);
            }
            else if (aheadCrossed)
            {
                // The first unmatched crossing can just mean the car ahead crossed first.
                var laps = ahead.CompletedLaps.Value - car.CompletedLaps.Value - 1;
                interval = laps > 0 ? new(laps, null) : state!.Interval;
            }
            else
            {
                interval = state!.Interval;
                // A delayed or corrected scoring time can arrive after the lap count.
                if (car.Time != state.CarTime && car.CompletedLaps == ahead.CompletedLaps)
                    interval = AtCarCrossing(car, ahead, false);
            }

            output[car.CarIdx] = interval;
            _intervals[car.CarIdx] = new DriverInterval(ahead.CarIdx, car.Time, interval);
        }

        foreach (var car in cars)
            if (car.CompletedLaps.HasValue) _previousLaps[car.CarIdx] = car.CompletedLaps;
        var present = cars.Select(car => car.CarIdx).ToHashSet();
        foreach (var carIdx in _previousLaps.Keys.Where(id => !present.Contains(id)).ToList()) _previousLaps.Remove(carIdx);
        foreach (var carIdx in _intervals.Keys.Where(id => !present.Contains(id)).ToList()) _intervals.Remove(carIdx);
        return output;
    }

    public void Reset()
    {
        _context = null;
        _previousLaps.Clear();
        _intervals.Clear();
    }

    private static SessionInterval AtCarCrossing(SessionIntervalInput car, SessionIntervalInput ahead, bool firstSnapshot)
    {
        var gap = ahead.CompletedLaps!.Value - car.CompletedLaps!.Value;
        // With no earlier crossing, unequal counts do not tell us which car has already crossed this lap.
        if (gap > 0) return firstSnapshot ? new(null, null) : new(gap, null);
        if (gap < 0) return new(null, null);
        var seconds = car.Time - ahead.Time;
        return seconds.HasValue && float.IsFinite(seconds.Value) && seconds >= 0
            ? new(0, seconds) : new(null, null);
    }

    private sealed record DriverInterval(int AheadCarIdx, float? CarTime, SessionInterval Interval);
}
