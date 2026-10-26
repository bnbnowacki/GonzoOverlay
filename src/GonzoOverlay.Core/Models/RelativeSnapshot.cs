namespace GonzoOverlay.Core.Models;

public sealed record RelativeSnapshot(
    int PlayerPosition,
    IReadOnlyList<RelativeEntry> Entries,
    DateTimeOffset Timestamp);

public sealed record RelativeEntry(
    int Position,
    string Driver,
    float? GapToPlayerSeconds,
    bool IsPlayer,
    bool IsOnPitRoad);
