using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GonzoOverlay.Infrastructure.iRacing;

// Logs only the SDK's parsed session information, independently of telemetry ticks.
internal sealed class SessionStandingsLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    private static readonly string[] DriverFields =
    [
        "CarIdx", "UserName", "CarNumber", "CarScreenName", "CarScreenNameShort",
        "LicString", "IRating", "CarIsPaceCar", "IsSpectator", "CarClassID", "CarID",
        "CarClassShortName", "CarClassEstLapTime", "UserID", "TeamID", "TeamName"
    ];
    private Dictionary<int, string> _previousResults = new();
    private Dictionary<int, string> _previousDrivers = new();
    private string? _context;
    private string? _sessionProperties;
    private string? _qualifying;
    private DateTimeOffset? _lastFullSnapshot;

    public string? Update(object sessionData, DateTimeOffset timestamp)
    {
        var info = Property(sessionData, "SessionInfo");
        var current = Property(info, "CurrentSessionNum");
        var weekend = Property(sessionData, "WeekendInfo");
        var session = Items(Property(info, "Sessions"))
            .FirstOrDefault(item => Equals(Property(item, "SessionNum"), current));
        var context = Serialize(new
        {
            CurrentSessionNum = current,
            SessionID = Property(weekend, "SessionID"),
            SubSessionID = Property(weekend, "SubSessionID"),
            SessionType = Property(session, "SessionType")
        });
        var full = _context != context || !_lastFullSnapshot.HasValue ||
            timestamp - _lastFullSnapshot.Value >= TimeSpan.FromMinutes(1);
        if (_context != context)
        {
            _previousResults = new();
            _previousDrivers = new();
            _sessionProperties = null;
            _qualifying = null;
            _context = context;
        }
        if (full) _lastFullSnapshot = timestamp;

        var prefix = $"{timestamp.ToLocalTime():HH:mm:ss.fff} | SDK-SESJA |";
        var lines = new List<string>();
        var sessionProperties = Serialize(ScalarProperties(session));
        var qualifying = Serialize(new
        {
            QualifyResults = Property(Property(sessionData, "QualifyResultsInfo"), "Results"),
            SessionQualifyPositions = Property(session, "QualifyPositions")
        });
        var results = ToRows(Items(Property(session, "ResultsPositions")), item => Serialize(item));
        var drivers = ToRows(Items(Property(Property(sessionData, "DriverInfo"), "Drivers")), item =>
            Serialize(DriverFields.ToDictionary(name => name, name => Property(item, name))));

        if (full || sessionProperties != _sessionProperties)
            lines.Add($"{prefix} SESJA | {sessionProperties}");
        if (full || qualifying != _qualifying)
            lines.Add($"{prefix} KWALIFIKACJE | {qualifying}");
        AppendChanges(lines, prefix, "WYNIK", results, _previousResults, full);
        AppendChanges(lines, prefix, "KIEROWCA", drivers, _previousDrivers, full);
        _sessionProperties = sessionProperties;
        _qualifying = qualifying;
        _previousResults = results;
        _previousDrivers = drivers;
        if (lines.Count == 0) return null;
        lines.Insert(0, $"{prefix} {(full ? "PELNY_OBRAZ" : "ZMIANY")} | {context}" +
            $" | wyniki={results.Count} kierowcy={drivers.Count} | sesja_dostepna={session is not null}");
        return string.Join(Environment.NewLine, lines);
    }

    public void Reset()
    {
        _context = null;
        _lastFullSnapshot = null;
        _previousResults.Clear();
        _previousDrivers.Clear();
        _sessionProperties = null;
        _qualifying = null;
    }

    private static void AppendChanges(List<string> lines, string prefix, string label,
        Dictionary<int, string> current, Dictionary<int, string> previous, bool full)
    {
        foreach (var (carIdx, value) in current.OrderBy(item => item.Key))
            if (full || !previous.TryGetValue(carIdx, out var old) || value != old)
                lines.Add($"{prefix} {label} carIdx={carIdx} | {value}");
        if (!full)
            foreach (var carIdx in previous.Keys.Except(current.Keys).Order())
                lines.Add($"{prefix} {label}_USUNIETY carIdx={carIdx}");
    }

    private static Dictionary<int, string> ToRows(IEnumerable<object> items, Func<object, string> serialize)
    {
        var rows = new Dictionary<int, string>();
        foreach (var item in items)
        {
            var value = Property(item, "CarIdx");
            if (value is not null && int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var carIdx))
                rows[carIdx] = serialize(item);
        }
        return rows;
    }

    private static Dictionary<string, object?> ScalarProperties(object? item) => item is null
        ? new()
        : item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0 &&
                (property.PropertyType.IsValueType || property.PropertyType == typeof(string)))
            .ToDictionary(property => property.Name, property => property.GetValue(item));

    private static string Serialize(object? value) => JsonSerializer.Serialize(value, JsonOptions);
    private static object? Property(object? item, string name) =>
        item?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(item);
    private static IEnumerable<object> Items(object? value) => value is IEnumerable items && value is not string
        ? items.Cast<object>() : Array.Empty<object>();
}
