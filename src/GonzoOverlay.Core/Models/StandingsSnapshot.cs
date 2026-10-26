namespace GonzoOverlay.Core.Models;

public sealed record StandingsSnapshot(
    string SessionType,
    IReadOnlyList<StandingEntry> Entries,
    DateTimeOffset Timestamp);

public sealed record StandingEntry(
    int Position,
    string Number,
    string Driver,
    string Car,
    float? LastLapSeconds,
    float? BestLapSeconds,
    int? LapsCompleted,
    int? LapsStarted,
    float? IntervalSeconds,
    float? LeaderGapSeconds,
    string SafetyRating,
    int? IRating);
