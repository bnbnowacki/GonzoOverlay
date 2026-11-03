namespace GonzoOverlay.Infrastructure.iRacing;

internal sealed class RaceStandingsSource
{
    private (int? Session, int? SessionId)? _context;
    private bool _raceLapSeen;
    private bool _raceResultsReady;

    public bool UseQualifying(int? session, int? sessionId, bool isRace, int? sessionState,
        bool hasQualifying, bool hasCompletedRaceLap, bool hasRacePositions)
    {
        var context = (session, sessionId);
        if (_context != context)
        {
            _context = context;
            _raceLapSeen = false;
            _raceResultsReady = false;
        }

        if (isRace && sessionState >= 4 && hasCompletedRaceLap)
            _raceLapSeen = true;
        if (_raceLapSeen && hasRacePositions)
            _raceResultsReady = true;

        // Once race data is ready, never revert to qualifying during this session.
        return isRace && hasQualifying && !_raceResultsReady;
    }

    public void Reset()
    {
        _context = null;
        _raceLapSeen = false;
        _raceResultsReady = false;
    }
}
