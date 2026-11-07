import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { StandingsSnapshot, StandingEntry } from '../overlay/types';
import { DriverRatingBadge } from './DriverRatingBadge';
import { selectStandingsRows } from './standingsRows';

type LapHighlight = 'none' | 'personalBest' | 'serverBest';

type LapEffect = {
  highlight: LapHighlight;
  version: number;
};

type LapEffects = Record<string, LapEffect>;

type PendingLap = {
  hasLapCompletedUpdate: boolean;
  hasLastLapUpdate: boolean;
  personalBest: boolean;
  serverBest: boolean;
  processed: boolean;
  appliedHighlight: LapHighlight;
  expiresAt: number;
};

type LapEvent = {
  key: string;
  highlight: LapHighlight;
};

const LAP_TIME_EPSILON_SECONDS = 0.001;
const LAP_CORRELATION_WINDOW_MS = 6000;

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

function getDriverKey(entry: StandingEntry) {
  return `${entry.number}-${entry.driver}`;
}

function getHighlightRank(highlight: LapHighlight) {
  return highlight === 'serverBest'
    ? 2
    : highlight === 'personalBest'
      ? 1
      : 0;
}

function isValidLapTime(seconds: number | null | undefined) {
  return seconds !== null && seconds !== undefined && seconds > 0;
}

export function StandingsWidget({ snapshot }: StandingsWidgetProps) {
  const standingsContext = `${snapshot.sessionType}-${snapshot.displayQualifyingResults}`;
  const previousSessionType = useRef(standingsContext);
  const previousLapCounts = useRef(new Map<string, number | null>());
  const previousLapStarts = useRef(new Map<string, number | null>());
  const previousLastLaps = useRef(new Map<string, number | null>());
  const previousBestLaps = useRef(new Map<string, number | null>());
  const pendingLaps = useRef(new Map<string, PendingLap>());
  const lapFlashTimers = useRef(new Map<string, ReturnType<typeof setTimeout>>());
  const [lapEffects, setLapEffects] = useState<LapEffects>({});
  const tableWrapRef = useRef<HTMLDivElement>(null);
  const rowHeightRef = useRef(0);
  const [rowCapacity, setRowCapacity] = useState<number | null>(null);
  const usesBestLap = snapshot.displayQualifyingResults || snapshot.sessionType.toLowerCase() !== 'race';

  useLayoutEffect(() => {
    const wrap = tableWrapRef.current;
    const table = wrap?.querySelector('table');
    if (!wrap || !table) return;

    const measure = () => {
      const row = table.querySelector('tbody tr');
      const rowHeight = row?.getBoundingClientRect().height || rowHeightRef.current;
      if (rowHeight > 0) {
        rowHeightRef.current = rowHeight;
        setRowCapacity(Math.floor((wrap.clientHeight + 0.01) / rowHeight));
      }
    };
    const observer = new ResizeObserver(measure);
    observer.observe(wrap);
    observer.observe(table);
    measure();
    return () => observer.disconnect();
  }, [snapshot.entries.length]);

  useEffect(() => {
    if (previousSessionType.current !== standingsContext) {
      previousSessionType.current = standingsContext;
      previousLapCounts.current = new Map();
      previousLapStarts.current = new Map();
      previousLastLaps.current = new Map();
      previousBestLaps.current = new Map();
      pendingLaps.current = new Map();
      for (const timer of lapFlashTimers.current.values()) {
        clearTimeout(timer);
      }
      lapFlashTimers.current.clear();
      setLapEffects({});
      return;
    }

    const now = Date.now();
    for (const [key, pending] of pendingLaps.current) {
      if (pending.expiresAt <= now) {
        pendingLaps.current.delete(key);
      }
    }

    const previousServerBestLap = Array.from(previousBestLaps.current.values())
      .filter((lap): lap is number => lap !== null && lap > 0)
      .reduce<number | null>(
        (best, lap) => best === null ? lap : Math.min(best, lap),
        null,
      );
    const serverBestLap = snapshot.entries
      .map((entry) => entry.bestLapSeconds)
      .filter((lap): lap is number => lap !== null && lap > 0)
      .reduce<number | null>(
        (best, lap) => best === null ? lap : Math.min(best, lap),
        null,
      );
    const nextLastLaps = new Map<string, number | null>();
    const nextBestLaps = new Map<string, number | null>();
    const nextLapCounts = new Map<string, number | null>();
    const nextLapStarts = new Map<string, number | null>();
    const lapEvents: LapEvent[] = [];

    for (const entry of snapshot.entries) {
      const key = getDriverKey(entry);
      const currentLast = entry.lastLapSeconds;
      const currentBest = entry.bestLapSeconds;
      const currentLapCount = entry.lapsCompleted;
      const currentLapStart = entry.lapsStarted;
      const previousLapCount = previousLapCounts.current.get(key);
      const previousLapStart = previousLapStarts.current.get(key);
      const previousLast = previousLastLaps.current.get(key);
      const previousBest = previousBestLaps.current.get(key);
      const currentBestIsValid = isValidLapTime(currentBest);
      const previousBestIsValid = isValidLapTime(previousBest);
      const lapCompletedUpdate =
        (currentLapCount !== null &&
          previousLapCount !== undefined &&
          previousLapCount !== null &&
          currentLapCount > previousLapCount) ||
        (currentLapStart !== null &&
          previousLapStart !== undefined &&
          previousLapStart !== null &&
          currentLapStart > previousLapStart);
      const lastLapUpdate =
        currentLast !== null &&
        previousLast !== undefined &&
        (previousLast === null ||
          Math.abs(currentLast - previousLast) > LAP_TIME_EPSILON_SECONDS);
      const improvedPersonalBest =
        currentBestIsValid &&
        (!previousBestIsValid ||
          currentBest! < previousBest! - LAP_TIME_EPSILON_SECONDS);
      const isServerBest =
        improvedPersonalBest &&
        currentBest !== null &&
        serverBestLap !== null &&
        currentBest <= serverBestLap + LAP_TIME_EPSILON_SECONDS;
      const isNewServerRecord =
        isServerBest &&
        (previousServerBestLap === null ||
          currentBest! < previousServerBestLap - LAP_TIME_EPSILON_SECONDS);

      if (usesBestLap && improvedPersonalBest && previousBest !== undefined) {
        lapEvents.push({ key, highlight: 'none' });
      }

      let pending = pendingLaps.current.get(key);
      if (!usesBestLap && (lapCompletedUpdate || lastLapUpdate)) {
        pending = pending ?? {
          hasLapCompletedUpdate: false,
          hasLastLapUpdate: false,
          personalBest: false,
          serverBest: false,
          processed: false,
          appliedHighlight: 'none',
          expiresAt: now + LAP_CORRELATION_WINDOW_MS,
        };
        pending.hasLapCompletedUpdate ||= lapCompletedUpdate;
        pending.hasLastLapUpdate ||= lastLapUpdate;
        pending.expiresAt = now + LAP_CORRELATION_WINDOW_MS;
        pendingLaps.current.set(key, pending);
      }

      if (!usesBestLap && pending) {
        pending.personalBest ||= improvedPersonalBest;
        pending.serverBest ||= isNewServerRecord;

        if (pending.hasLapCompletedUpdate && pending.hasLastLapUpdate) {
          const highlight = pending.serverBest
            ? 'serverBest'
            : pending.personalBest
              ? 'personalBest'
              : 'none';
          const shouldApply = !pending.processed ||
            getHighlightRank(highlight) > getHighlightRank(pending.appliedHighlight);

          if (shouldApply) {
            lapEvents.push({ key, highlight });
            pending.processed = true;
            pending.appliedHighlight = highlight;
          }
        }
      }

      nextLapCounts.set(key, currentLapCount);
      nextLapStarts.set(key, currentLapStart);
      nextLastLaps.set(key, currentLast);
      nextBestLaps.set(key, currentBest);
    }

    previousLapCounts.current = nextLapCounts;
    previousLapStarts.current = nextLapStarts;
    previousLastLaps.current = nextLastLaps;
    previousBestLaps.current = nextBestLaps;

    if (lapEvents.length === 0) {
      return;
    }

    const nextEffects = new Map<string, LapEffect>();
    const hasNewServerRecord = lapEvents.some(
      ({ highlight }) => highlight === 'serverBest',
    );

    if (hasNewServerRecord) {
      for (const [key, effect] of Object.entries(lapEffects)) {
        if (effect.highlight === 'serverBest') {
          nextEffects.set(key, {
            ...effect,
            highlight: 'personalBest',
          });
        }
      }
    }

    for (const { key, highlight } of lapEvents) {
      nextEffects.set(key, {
        highlight,
        version: (lapEffects[key]?.version ?? 0) + 1,
      });
    }

    setLapEffects((current) => {
      const next = { ...current };
      for (const [key, effect] of nextEffects) {
        next[key] = effect;
      }
      return next;
    });

    for (const { key, highlight } of lapEvents) {
      const previousTimer = lapFlashTimers.current.get(key);
      if (previousTimer) {
        clearTimeout(previousTimer);
      }

      if (highlight !== 'none') {
        lapFlashTimers.current.delete(key);
        continue;
      }

      const effect = nextEffects.get(key);
      if (!effect) {
        continue;
      }

      lapFlashTimers.current.set(key, setTimeout(() => {
        setLapEffects((current) => {
          if (current[key]?.version !== effect.version) {
            return current;
          }

          const next = { ...current };
          delete next[key];
          return next;
        });
        lapFlashTimers.current.delete(key);
      }, 1200));
    }
  }, [snapshot]);

  useEffect(
    () => () => {
      for (const timer of lapFlashTimers.current.values()) {
        clearTimeout(timer);
      }
    },
    [],
  );

  return (
    <div className="standings-widget" aria-label="Standings">
      <div className="standings-table-wrap" ref={tableWrapRef}>
        <table className="standings-table">
          <tbody>
            {(rowCapacity === null ? snapshot.entries : selectStandingsRows(snapshot.entries, rowCapacity)).map((entry, index, visibleEntries) => {
              const key = getDriverKey(entry);
              const lapTime = usesBestLap ? entry.bestLapSeconds : entry.lastLapSeconds;
              const lapEffect = lapTime !== null ? lapEffects[key] : undefined;
              const flashClass = lapEffect
                ? lapEffect.highlight === 'serverBest'
                  ? 'standings-lap-flash-purple'
                  : 'standings-lap-flash-green'
                : '';
              const persistentClass = lapEffect?.highlight === 'serverBest'
                ? 'standings-lap-server-best'
                : lapEffect?.highlight === 'personalBest'
                  ? 'standings-lap-personal-best'
                  : '';

              return (
                <tr key={key} className={[
                  entry.isPlayer ? 'standings-player' : '',
                  index > 0 && entry.position > visibleEntries[index - 1].position + 1 ? 'standings-row-gap' : '',
                ].filter(Boolean).join(' ') || undefined}>
                  <td className="standings-position">{entry.position}</td>
                  <td className="standings-driver">{entry.driver}</td>
                  <td className="standings-number">{entry.number}</td>
                  <td className="standings-rating"><DriverRatingBadge safetyRating={entry.safetyRating} iRating={entry.iRating} /></td>
                  <td className="standings-car">{entry.car}</td>
                  <td>{entry.position === 1
                    ? 'INT'
                    : !usesBestLap && entry.lapsBehindAhead != null && entry.lapsBehindAhead > 0
                      ? `+${entry.lapsBehindAhead} laps`
                      : formatGap(entry.intervalSeconds)}</td>
                  <td>{entry.position === 1
                    ? 'LEADER'
                    : !usesBestLap && entry.lapsBehindLeader != null && entry.lapsBehindLeader > 0
                      ? `+${entry.lapsBehindLeader} laps`
                      : formatGap(entry.leaderGapSeconds)}</td>
                  <td
                    key={`${key}-${lapEffect?.version ?? 0}`}
                    className={`${flashClass} ${persistentClass}`.trim() || undefined}
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
