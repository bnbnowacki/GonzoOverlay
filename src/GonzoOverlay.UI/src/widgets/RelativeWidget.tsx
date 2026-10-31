import type { RelativeSnapshot } from '../overlay/types';

type RelativeWidgetProps = {
  snapshot: RelativeSnapshot;
};

function formatGap(seconds: number | null, isPlayer: boolean) {
  if (isPlayer) {
    return '—';
  }

  if (seconds === null || !Number.isFinite(seconds)) {
    return '--';
  }

  return `${seconds > 0 ? '+' : ''}${seconds.toFixed(1)}`;
}

function formatPosition(position: number, isPlayer: boolean) {
  if (isPlayer) {
    return position > 0 ? position : 'YOU';
  }

  return position > 0 ? position : '--';
}

function isValidSessionLaps(laps: number | null) {
  return laps !== null && Number.isFinite(laps) && laps > 0 && laps < 32767;
}

function formatSessionLaps(laps: number | null) {
  return isValidSessionLaps(laps) ? String(Math.trunc(laps)) : 'shit';
}

function getTimedRaceEstimate(laps: number | null) {
  if (laps === null || !Number.isFinite(laps) || laps <= 0) {
    return null;
  }

  const wholeLaps = Math.floor(laps);
  return {
    totalLaps: wholeLaps + 1,
    extraLapIndicator: Math.round((laps - wholeLaps) * 100),
  };
}

function formatCurrentLap(lap: number | null) {
  if (lap === null || !Number.isFinite(lap) || lap < 0) {
    return '--';
  }

  return String(Math.trunc(lap));
}

function formatRemainingTime(seconds: number | null) {
  if (seconds === null || !Number.isFinite(seconds) || seconds < 0) {
    return '--';
  }

  const totalSeconds = Math.floor(seconds);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const remainder = totalSeconds % 60;

  return hours > 0
    ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remainder).padStart(2, '0')}`
    : `${minutes}:${String(remainder).padStart(2, '0')}`;
}

export function RelativeWidget({ snapshot }: RelativeWidgetProps) {
  const isLapBasedRace = isValidSessionLaps(snapshot.sessionLapsTotal);
  const timedRaceEstimate = getTimedRaceEstimate(snapshot.estimatedRaceLapsTotal);
  const playerIndex = snapshot.entries.findIndex((entry) => entry.isPlayer);
  const ahead = playerIndex >= 0 ? snapshot.entries.slice(0, playerIndex) : [];
  const behind = playerIndex >= 0 ? snapshot.entries.slice(playerIndex + 1) : [];
  const rows = [
    ahead[2] ?? null,
    ahead[1] ?? null,
    ahead[0] ?? null,
    playerIndex >= 0 ? snapshot.entries[playerIndex] : null,
    behind[0] ?? null,
    behind[1] ?? null,
    behind[2] ?? null,
  ];

  return (
    <section className="relative-widget">
      <table className="relative-table">
        <tbody>
          {rows.map((entry, index) => {
            if (!entry) {
              return (
                <tr className="relative-empty" key={`empty-${index}`}>
                  <td />
                  <td />
                  <td />
                </tr>
              );
            }

            return (
              <tr
                key={`${entry.position}-${entry.driver}`}
                className={[
                  entry.isPlayer ? 'relative-player' : '',
                  entry.isOnPitRoad ? 'relative-pit' : '',
                ].filter(Boolean).join(' ') || undefined}
              >
                <td className="relative-position">
                  {formatPosition(entry.position, entry.isPlayer)}
                </td>
                <td className={[
                  'relative-driver',
                  !entry.isPlayer && entry.lapRelation === 'ahead' ? 'relative-driver-lapping' : '',
                  !entry.isPlayer && entry.lapRelation === 'behind' ? 'relative-driver-lapped' : '',
                ].filter(Boolean).join(' ')}>{entry.driver}</td>
                <td className="relative-gap">
                  {formatGap(entry.gapToPlayerSeconds, entry.isPlayer)}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <div className={`relative-footer${isLapBasedRace ? ' relative-footer-laps-only' : ''}`}>
        {isLapBasedRace ? (
          <div className="relative-footer-item relative-footer-laps-only-item">
            <span>OKRĄŻENIA</span>
            <strong>
              {formatCurrentLap(snapshot.currentLap)} / {formatSessionLaps(snapshot.sessionLapsTotal)}
            </strong>
          </div>
        ) : (
          <>
            <div className="relative-footer-lap-group">
              <div className="relative-footer-item">
                <span>OKRĄŻENIA</span>
                <strong>
                  {formatCurrentLap(snapshot.currentLap)} / {timedRaceEstimate?.totalLaps ?? '--'}
                </strong>
              </div>
              <div className="relative-footer-item">
                <span>+1 OKR</span>
                <strong
                  style={timedRaceEstimate
                    ? { color: `hsl(${120 - timedRaceEstimate.extraLapIndicator * 1.2} 85% 60%)` }
                    : undefined}
                >
                  {timedRaceEstimate ? `${timedRaceEstimate.extraLapIndicator}%` : '--'}
                </strong>
              </div>
            </div>
            <div className="relative-footer-item relative-footer-time">
              <span>CZAS</span>
              <strong>
                {snapshot.raceFinished ? 'meta' : formatRemainingTime(snapshot.sessionTimeRemainingSeconds)} /{' '}
                {formatRemainingTime(snapshot.sessionTimeTotalSeconds)}
              </strong>
            </div>
          </>
        )}
      </div>
    </section>
  );
}
