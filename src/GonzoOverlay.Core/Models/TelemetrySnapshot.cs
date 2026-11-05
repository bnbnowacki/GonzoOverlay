namespace GonzoOverlay.Core.Models;

public sealed record TelemetrySnapshot(
    float? SpeedMetersPerSecond,
    float? SpeedKmh,
    float? PitSpeedLimitMetersPerSecond,
    bool? OnPitRoad,
    bool? IsApproachingPits,
    bool? IsOnTrack,
    float? Rpm,
    int? Gear,
    float? FuelLevelLiters,
    DateTimeOffset Timestamp);

public sealed record WeatherSnapshot(
    string TrackWetness,
    DateTimeOffset Timestamp);
