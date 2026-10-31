namespace GonzoOverlay.Infrastructure.iRacing;

// Standings lap gaps change only when the corresponding driver crosses the line.
internal sealed class StandingsLapGap
{
    private readonly Dictionary<int, DriverState> _drivers = new();
    private int? _sessionNumber;
    private int? _sessionId;
    private bool _isRace;

    public void BeginSession(int? sessionNumber, int? sessionId, bool isRace)
    {
        if (_sessionNumber != sessionNumber || _sessionId != sessionId || _isRace != isRace)
        {
            Reset();
            _sessionNumber = sessionNumber;
            _sessionId = sessionId;
            _isRace = isRace;
        }
    }

    public StandingsLapGaps UpdateDriver(
        int carIdx, double? completedLaps, double? leaderCompletedLaps,
        int? aheadCarIdx = null, double? aheadCompletedLaps = null)
    {
        if (!_isRace)
        {
            return new(null, null);
        }

        if (!IsValidLapCount(completedLaps))
        {
            return _drivers.TryGetValue(carIdx, out var existing)
                ? GetGaps(existing, aheadCarIdx)
                : new(null, null);
        }

        var completed = (int)completedLaps!.Value;
        if (!_drivers.TryGetValue(carIdx, out var state) || completed < state.CompletedLaps)
        {
            _drivers[carIdx] = new DriverState(completed);
            return new(null, null);
        }

        if (completed > state.CompletedLaps)
        {
            state.CompletedLaps = completed;
            if (IsValidLapCount(leaderCompletedLaps))
            {
                state.LapsBehindLeader = Math.Max(0, (int)leaderCompletedLaps!.Value - completed);
            }

            state.AheadCarIdx = aheadCarIdx;
            state.LapsBehindAhead = aheadCarIdx.HasValue && IsValidLapCount(aheadCompletedLaps)
                ? Math.Max(0, (int)aheadCompletedLaps!.Value - completed)
                : null;
        }

        return GetGaps(state, aheadCarIdx);
    }

    private static StandingsLapGaps GetGaps(DriverState state, int? aheadCarIdx) =>
        new(state.LapsBehindLeader,
            aheadCarIdx.HasValue && aheadCarIdx == state.AheadCarIdx ? state.LapsBehindAhead : null);

    public void Reset()
    {
        _drivers.Clear();
        _sessionNumber = null;
        _sessionId = null;
        _isRace = false;
    }

    private static bool IsValidLapCount(double? laps) =>
        laps.HasValue && double.IsFinite(laps.Value) &&
        laps.Value >= 0 && laps.Value <= int.MaxValue && Math.Truncate(laps.Value) == laps.Value;

    private sealed class DriverState(int completedLaps)
    {
        public int CompletedLaps { get; set; } = completedLaps;
        public int? LapsBehindLeader { get; set; }
        public int? AheadCarIdx { get; set; }
        public int? LapsBehindAhead { get; set; }
    }
}

internal readonly record struct StandingsLapGaps(int? Leader, int? Ahead);
