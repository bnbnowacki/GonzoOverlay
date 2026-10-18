namespace GonzoOverlay.Core.Models;

public sealed record TelemetrySnapshot(
    float? SpeedMetersPerSecond,
    float? SpeedKmh,
    float? Rpm,
    int? Gear,
    float? FuelLevelLiters,
    DateTimeOffset Timestamp);

public sealed record WeatherSnapshot(
    string TrackWetness,
    DateTimeOffset Timestamp);
