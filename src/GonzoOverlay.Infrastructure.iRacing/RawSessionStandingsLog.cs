using System.Text.RegularExpressions;

namespace GonzoOverlay.Infrastructure.iRacing;

// Periodic unparsed SDK sections reveal fields missing from the typed SDK model.
internal sealed class RawSessionStandingsLog
{
    private DateTimeOffset? _lastSnapshot;
    private string? _sessionNumber;

    public string? Update(string yaml, DateTimeOffset timestamp)
    {
        var sessionInfo = ExtractSection(yaml, "SessionInfo");
        if (sessionInfo is null) return null;
        var sessionNumber = Regex.Match(sessionInfo, @"(?m)^\s+CurrentSessionNum:\s*([^\r\n#]+)").Groups[1].Value.Trim();
        if (_lastSnapshot.HasValue && sessionNumber == _sessionNumber &&
            timestamp - _lastSnapshot.Value < TimeSpan.FromMinutes(1)) return null;
        _lastSnapshot = timestamp;
        _sessionNumber = sessionNumber;
        var qualifying = ExtractSection(yaml, "QualifyResultsInfo");
        return $"{timestamp.ToLocalTime():HH:mm:ss.fff} | SDK-SESJA | SUROWY_YAML | CurrentSessionNum={sessionNumber}" +
            Environment.NewLine + sessionInfo +
            (qualifying is null ? "" : Environment.NewLine + qualifying);
    }

    public void Reset()
    {
        _lastSnapshot = null;
        _sessionNumber = null;
    }

    private static string? ExtractSection(string yaml, string key)
    {
        var start = Regex.Match(yaml, $@"(?m)^{Regex.Escape(key)}:[^\r\n]*\r?\n");
        if (!start.Success) return null;
        var next = Regex.Match(yaml[(start.Index + start.Length)..], @"(?m)^[A-Za-z][A-Za-z0-9_]*:");
        var end = next.Success ? start.Index + start.Length + next.Index : yaml.Length;
        return yaml[start.Index..end].TrimEnd();
    }
}
