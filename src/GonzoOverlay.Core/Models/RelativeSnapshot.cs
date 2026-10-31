namespace GonzoOverlay.Core.Models;

public sealed record RelativeSnapshot(
    int PlayerPosition,
    IReadOnlyList<RelativeEntry> Entries,
    int? CurrentLap,
    int? SessionLapsTotal,
    double? EstimatedRaceLapsTotal,
    bool RaceFinished,
    double? SessionTimeRemainingSeconds,
    double? SessionTimeTotalSeconds,
    DateTimeOffset Timestamp);

public sealed record RelativeEntry(
    int Position,
    string Driver,
    float? GapToPlayerSeconds,
    bool IsPlayer,
    bool IsOnPitRoad,
    string LapRelation);
