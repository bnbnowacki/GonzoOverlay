namespace GonzoOverlay.Infrastructure.iRacing;

internal static class RelativeLapRelation
{
    public static string Calculate(
        double? playerCompleted, double? driverCompleted,
        double? playerProgress, double? driverProgress, float? relativeGap)
    {
        if (!IsValidLapCount(playerCompleted) || !IsValidLapCount(driverCompleted) ||
            !IsValidProgress(playerProgress) || !IsValidProgress(driverProgress) ||
            !relativeGap.HasValue || !float.IsFinite(relativeGap.Value) || relativeGap.Value == 0)
        {
            return "unknown";
        }

        var lapOffset = (int)driverCompleted!.Value - (int)playerCompleted!.Value;

        // Relative shows the shortest route around the circuit. Correct the lap
        // difference when that route crosses the start/finish line.
        if (relativeGap.Value > 0 && driverProgress!.Value < playerProgress!.Value)
        {
            lapOffset--;
        }
        else if (relativeGap.Value < 0 && driverProgress!.Value > playerProgress!.Value)
        {
            lapOffset++;
        }

        return lapOffset > 0 ? "ahead" : lapOffset < 0 ? "behind" : "same";
    }

    private static bool IsValidLapCount(double? laps) =>
        laps.HasValue && double.IsFinite(laps.Value) && laps.Value >= 0 &&
        laps.Value <= int.MaxValue && Math.Truncate(laps.Value) == laps.Value;

    private static bool IsValidProgress(double? progress) =>
        progress.HasValue && double.IsFinite(progress.Value) && progress.Value >= 0 && progress.Value <= 1;
}
