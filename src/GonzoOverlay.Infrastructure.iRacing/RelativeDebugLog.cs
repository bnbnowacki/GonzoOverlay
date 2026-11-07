using System.Globalization;

namespace GonzoOverlay.Infrastructure.iRacing;

internal sealed record RelativeDebugCar(
    int CarIdx, string Name, int Position, double? CompletedLaps, double? Progress,
    float? EstTime, float? ClassLapTime, float Gap, string Relation, bool OnPitRoad);

// Sample nearby cars, and capture every relation/sign transition even between samples.
internal sealed class RelativeDebugLog
{
    private const double RangeSeconds = 3;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);
    private DateTimeOffset? _lastSample;
    private string? _context;
    private Dictionary<int, (string Relation, int Sign)> _previous = new();

    public string? Update(DateTimeOffset timestamp, string context, int? currentLap,
        RelativeDebugCar player, IReadOnlyList<RelativeDebugCar> cars)
    {
        if (_context != context)
        {
            Reset();
            _context = context;
        }
        var nearby = cars.Where(car => float.IsFinite(car.Gap) && Math.Abs(car.Gap) <= RangeSeconds)
            .OrderBy(car => car.Gap).ToList();
        var current = nearby.ToDictionary(car => car.CarIdx, car => (car.Relation, Math.Sign(car.Gap)));
        var changed = current.Count != _previous.Count || current.Any(pair =>
            !_previous.TryGetValue(pair.Key, out var old) || old != pair.Value);
        _previous = current;
        if (nearby.Count == 0 || (!changed && _lastSample.HasValue && timestamp - _lastSample < SampleInterval))
            return null;
        _lastSample = timestamp;

        static string Number(double? value) => value.HasValue && double.IsFinite(value.Value)
            ? value.Value.ToString("0.000000", CultureInfo.InvariantCulture) : "brak";
        static string Car(RelativeDebugCar car) =>
            $"id={car.CarIdx} nazwa={car.Name} pozycja={car.Position} ukończone={Number(car.CompletedLaps)}" +
            $" dystans={Number(car.Progress)} estTime={Number(car.EstTime)} estLap={Number(car.ClassLapTime)} pit={car.OnPitRoad}";

        var competitors = string.Join(" | ", nearby.Select(car =>
            $"RYWAL [{Car(car)} gap={Number(car.Gap)} relacja={car.Relation}]"));
        return $"{timestamp.ToLocalTime():HH:mm:ss.fff} | RELATIVE-DEBUG | {context}" +
            $" | lap={currentLap?.ToString() ?? "brak"} | GRACZ [{Car(player)}] | {competitors}";
    }

    public void Reset()
    {
        _lastSample = null;
        _context = null;
        _previous.Clear();
    }
}
