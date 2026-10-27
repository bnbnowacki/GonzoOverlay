namespace GonzoOverlay.Core.Models;

public sealed record FuelSnapshot(
    string SessionType,
    float? CurrentFuelLiters,
    float? AverageConsumptionPerLapLiters,
    float? LapsOfFuelRemaining,
    float? FuelToAddLiters,
    DateTimeOffset Timestamp);
