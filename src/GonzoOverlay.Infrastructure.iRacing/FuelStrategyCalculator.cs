namespace GonzoOverlay.Infrastructure.iRacing;

internal static class FuelStrategyCalculator
{
    private const int FuelFillFlag = 0x10;

    public static (float? ToAdd, float? AtFinish) Calculate(
        bool isRace,
        int? sessionLapsTotal,
        double? estimatedRaceLapsTotal,
        int? currentLap,
        double? lapProgress,
        float? averageConsumption,
        float? currentFuel,
        int? pitServiceFlags,
        float? plannedFuel)
    {
        if (!isRace || currentLap is not > 0 ||
            !lapProgress.HasValue || !double.IsFinite(lapProgress.Value) ||
            lapProgress.Value < 0 || lapProgress.Value > 1 ||
            !averageConsumption.HasValue || !float.IsFinite(averageConsumption.Value) ||
            averageConsumption.Value <= 0 ||
            !currentFuel.HasValue || !float.IsFinite(currentFuel.Value) || currentFuel.Value < 0)
        {
            return (null, null);
        }

        double? raceLaps = sessionLapsTotal is > 0 and < 32767
            ? sessionLapsTotal.Value
            : estimatedRaceLapsTotal is > 0 && double.IsFinite(estimatedRaceLapsTotal.Value)
                ? Math.Floor(estimatedRaceLapsTotal.Value) + 1
                : null;
        if (!raceLaps.HasValue)
        {
            return (null, null);
        }

        var lapsRemaining = Math.Max(0, raceLaps.Value - (currentLap.Value - 1 + lapProgress.Value));
        var fuelRequired = lapsRemaining * averageConsumption.Value;
        var toAdd = (float)Math.Max(0, fuelRequired - currentFuel.Value);
        if (!pitServiceFlags.HasValue)
        {
            return (toAdd, null);
        }

        var fuelFillEnabled = (pitServiceFlags.Value & FuelFillFlag) != 0;
        if (fuelFillEnabled && (!plannedFuel.HasValue ||
            !float.IsFinite(plannedFuel.Value) || plannedFuel.Value < 0))
        {
            return (toAdd, null);
        }

        var scheduledFuel = fuelFillEnabled ? plannedFuel!.Value : 0;
        return (toAdd, (float)(currentFuel.Value + scheduledFuel - fuelRequired));
    }
}
