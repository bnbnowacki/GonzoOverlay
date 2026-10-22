import { useEffect, useRef, useState } from 'react';
import type { StandingsSnapshot, StandingEntry } from '../overlay/types';

type PulseVersions = Record<string, number>;

type StandingsWidgetProps = {
  snapshot: StandingsSnapshot;
};

function formatLapTime(seconds: number | null) {
  if (seconds === null || seconds <= 0) {
    return '--';
  }

  const minutes = Math.floor(seconds / 60);
  const remainder = (seconds - minutes * 60).toFixed(3).padStart(6, '0');
  return `${minutes}:${remainder}`;
}

function formatGap(seconds: number | null) {
  if (seconds === null || seconds < 0) {
    return '--';
  }

  return `+${seconds.toFixed(3)}`;
}

function formatIRating(irating: number | null) {
  if (irating === null) {
    return '--';
  }

  return irating >= 1000
    ? `${(irating / 1000).toFixed(1).replace('.0', '')}k`
    : String(irating);
}

function getDriverKey(entry: StandingEntry) {
  return `${entry.number}-${entry.driver}`;
}

export function StandingsWidget({ snapshot }: StandingsWidgetProps) {
  const previousSessionType = useRef(snapshot.sessionType);
  const previousBestLaps = useRef(new Map<string, number | null>());
  const pulseTimers = useRef(new Map<string, ReturnType<typeof setTimeout>>());
  const [pulseVersions, setPulseVersions] = useState<PulseVersions>({});
  const usesBestLap = snapshot.sessionType.toLowerCase() !== 'race';

  useEffect(() => {
    if (previousSessionType.current !== snapshot.sessionType) {
      previousSessionType.current = snapshot.sessionType;
      previousBestLaps.current = new Map();
      setPulseVersions({});
      return;
    }

    const nextBestLaps = new Map<string, number | null>();
    const improvedDrivers: string[] = [];

    for (const entry of snapshot.entries) {
      const key = getDriverKey(entry);
      const currentBest = entry.bestLapSeconds;
      const previousBest = previousBestLaps.current.get(key);

      if (
        currentBest !== null &&
        previousBest !== undefined &&
        previousBest !== null &&
        currentBest < previousBest - 0.001
      ) {
        improvedDrivers.push(key);
      }

      nextBestLaps.set(key, currentBest);
    }

    previousBestLaps.current = nextBestLaps;

    if (improvedDrivers.length === 0) {
      return;
    }

    setPulseVersions((current) => {
      const next = { ...current };
      for (const key of improvedDrivers) {
        next[key] = (next[key] ?? 0) + 1;
      }
      return next;
    });

    for (const key of improvedDrivers) {
      const previousTimer = pulseTimers.current.get(key);
      if (previousTimer) {
        clearTimeout(previousTimer);
      }

      pulseTimers.current.set(
        key,
        setTimeout(() => {
          setPulseVersions((current) => {
            const next = { ...current };
            delete next[key];
            return next;
          });
          pulseTimers.current.delete(key);
        }, 13000),
      );
    }
  }, [snapshot]);

  useEffect(
    () => () => {
      for (const timer of pulseTimers.current.values()) {
        clearTimeout(timer);
      }
    },
    [],
  );

  return (
    <div className="standings-widget" aria-label="Standings">
      <div className="standings-table-wrap">
        <table className="standings-table">
          <tbody>
            {snapshot.entries.map((entry) => {
              const key = getDriverKey(entry);
              const lapTime = usesBestLap ? entry.bestLapSeconds : entry.lastLapSeconds;
              const isPulsing = usesBestLap && pulseVersions[key] !== undefined;

              return (
                <tr key={key}>
                  <td className="standings-position">{entry.position}</td>
                  <td className="standings-driver">{entry.driver}</td>
                  <td className="standings-number">{entry.number}</td>
                  <td>{entry.safetyRating || '--'}</td>
                  <td>{formatIRating(entry.iRating)}</td>
                  <td className="standings-car">{entry.car}</td>
                  <td>{entry.position === 1 ? 'INT' : formatGap(entry.intervalSeconds)}</td>
                  <td>{entry.position === 1 ? 'LEADER' : formatGap(entry.leaderGapSeconds)}</td>
                  <td
                    key={`${key}-${pulseVersions[key] ?? 0}`}
                    className={isPulsing ? 'standings-lap-pulse' : undefined}
                  >
                    {formatLapTime(lapTime)}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
