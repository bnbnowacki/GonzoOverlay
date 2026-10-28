namespace GonzoOverlay.Infrastructure.iRacing;

// The SDK exposes the latest lap, so history has to be collected while connected.
internal sealed class LeaderLapEstimator
{
    private readonly Dictionary<int, CarState> _cars = new();
    private readonly Queue<double> _leaderLaps = new();
    private int? _sessionNumber;
    private int? _sessionId;
    private PendingLap? _pendingLap;
    private double? _estimatedTotal;
    public bool IsFinished { get; private set; }

    public double? Update(
        int? sessionNumber,
        int? sessionId,
        bool isTimedRace,
        bool raceFinished,
        IReadOnlyList<double?> positions,
        IReadOnlyList<double?> completedLaps,
        IReadOnlyList<double?> lastLapTimes,
        IReadOnlyList<bool?> onPitRoad,
        double? timeRemaining,
        DateTimeOffset timestamp)
    {
        if (_sessionNumber != sessionNumber || _sessionId != sessionId)
        {
            _cars.Clear();
            _leaderLaps.Clear();
            _pendingLap = null;
            _estimatedTotal = null;
            IsFinished = false;
            _sessionNumber = sessionNumber;
            _sessionId = sessionId;
        }

        if (!isTimedRace)
        {
            return null;
        }

        if (raceFinished)
        {
            IsFinished = true;
            _pendingLap = null;
        }

        if (IsFinished)
        {
            return _estimatedTotal;
        }

        for (var index = 0; index < completedLaps.Count; index++)
        {
            var completed = completedLaps[index];
            if (!completed.HasValue || completed.Value < 0)
            {
                continue;
            }

            var lastLap = index < lastLapTimes.Count ? lastLapTimes[index] : null;
            var pit = index < onPitRoad.Count ? onPitRoad[index] : null;
            var position = index < positions.Count ? positions[index] : null;
            if (!_cars.TryGetValue(index, out var car))
            {
                // Wait for a complete lap observed from start to finish.
                _cars[index] = new CarState((int)completed.Value, lastLap, true);
                continue;
            }

            if (completed.Value < car.CompletedLaps)
            {
                _cars[index] = new CarState((int)completed.Value, lastLap, true);
                continue;
            }

            car.PitOrUnknown |= pit != false;
            var changed = lastLap is > 0 && double.IsFinite(lastLap.Value) &&
                (!car.LastLap.HasValue || Math.Abs(lastLap.Value - car.LastLap.Value) > 0.0005);
            if (completed.Value > car.CompletedLaps)
            {
                var isLeader = position == 1;
                if (isLeader && timeRemaining is >= 0 && double.IsFinite(timeRemaining.Value))
                {
                    UpdateEstimate((int)completed.Value, timeRemaining.Value);
                    _pendingLap = !car.PitOrUnknown && completed.Value == car.CompletedLaps + 1
                        ? new PendingLap(index, (int)completed.Value, timeRemaining.Value, timestamp)
                        : null;
                }

                car.CompletedLaps = (int)completed.Value;
                car.PitOrUnknown = pit != false;
            }

            if (_pendingLap is { } pending && pending.CarIdx == index && changed &&
                timestamp - pending.CrossedAt <= TimeSpan.FromSeconds(6))
            {
                _leaderLaps.Enqueue(lastLap!.Value);
                while (_leaderLaps.Count > 5)
                {
                    _leaderLaps.Dequeue();
                }

                UpdateEstimate(pending.CompletedLaps, pending.TimeRemaining);
                _pendingLap = null;
            }

            car.LastLap = lastLap;
        }

        if (_pendingLap is { } expired && timestamp - expired.CrossedAt > TimeSpan.FromSeconds(6))
        {
            _pendingLap = null;
        }

        return _estimatedTotal;
    }

    public void Reset()
    {
        _cars.Clear();
        _leaderLaps.Clear();
        _pendingLap = null;
        _estimatedTotal = null;
        IsFinished = false;
        _sessionNumber = null;
        _sessionId = null;
    }

    private void UpdateEstimate(int completedLaps, double timeRemaining)
    {
        if (_leaderLaps.Count < 3)
        {
            return;
        }

        var leaderAverageLap = _leaderLaps.OrderBy(lap => lap).Take(3).Average();
        _estimatedTotal = completedLaps + timeRemaining / leaderAverageLap;
    }

    private sealed class CarState(int completedLaps, double? lastLap, bool pitOrUnknown)
    {
        public int CompletedLaps { get; set; } = completedLaps;
        public double? LastLap { get; set; } = lastLap;
        public bool PitOrUnknown { get; set; } = pitOrUnknown;
    }

    private sealed record PendingLap(
        int CarIdx,
        int CompletedLaps,
        double TimeRemaining,
        DateTimeOffset CrossedAt);
}
