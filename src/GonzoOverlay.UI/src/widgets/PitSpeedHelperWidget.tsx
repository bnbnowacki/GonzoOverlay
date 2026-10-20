import { useEffect, useRef, useState } from 'react';
import type { TelemetrySnapshot } from '../overlay/types';

const DECREASING_SAMPLES_REQUIRED = 3;
const PIT_SPEED_RANGE_KMH = 10;
const STOPPED_SPEED_THRESHOLD_KMH = 0.5;

type PitSpeedHelperMode =
  | 'hidden'
  | 'prePitlane'
  | 'inPitsPrePitstop'
  | 'afterPitstop';

type PitSpeedHelperWidgetProps = {
  snapshot: TelemetrySnapshot;
};

export function PitSpeedHelperWidget({ snapshot }: PitSpeedHelperWidgetProps) {
  const previousSpeedRef = useRef<number | null>(null);
  const highestSpeedRef = useRef<number | null>(null);
  const consecutiveDecreasingSamplesRef = useRef(0);
  const brakingStartSpeedRef = useRef<number | null>(null);
  const modeRef = useRef<PitSpeedHelperMode>('hidden');
  const [mode, setMode] = useState<PitSpeedHelperMode>('hidden');
  const [brakingStartSpeed, setBrakingStartSpeed] = useState<number | null>(null);

  useEffect(() => {
    const speed = snapshot.speedMetersPerSecond;
    const isApproachingPits = snapshot.isApproachingPits === true;
    const onPitRoad = snapshot.onPitRoad === true;
    const pitLimit = snapshot.pitSpeedLimitMetersPerSecond;
    const speedKmh = speed !== null ? speed * 3.6 : null;
    const isStopped = speedKmh !== null && speedKmh <= STOPPED_SPEED_THRESHOLD_KMH;

    if (!isApproachingPits && !onPitRoad) {
      modeRef.current = 'hidden';
      setMode('hidden');
      previousSpeedRef.current = null;
      highestSpeedRef.current = null;
      consecutiveDecreasingSamplesRef.current = 0;
      brakingStartSpeedRef.current = null;
      setBrakingStartSpeed(null);
      return;
    }

    if (
      modeRef.current !== 'afterPitstop' &&
      onPitRoad &&
      isStopped &&
      pitLimit !== null
    ) {
      modeRef.current = 'afterPitstop';
      setMode('afterPitstop');
      return;
    }

    if (
      modeRef.current === 'afterPitstop' &&
      onPitRoad &&
      speed !== null &&
      pitLimit !== null &&
      speed > pitLimit - PIT_SPEED_RANGE_KMH / 3.6
    ) {
      modeRef.current = 'inPitsPrePitstop';
      setMode('inPitsPrePitstop');
      return;
    }

    if (modeRef.current === 'afterPitstop') {
      return;
    }

    if (
      modeRef.current !== 'inPitsPrePitstop' &&
      onPitRoad &&
      speed !== null &&
      pitLimit !== null &&
      speed < pitLimit
    ) {
      modeRef.current = 'inPitsPrePitstop';
      setMode('inPitsPrePitstop');
      return;
    }

    if (modeRef.current === 'hidden' && isApproachingPits) {
      modeRef.current = 'prePitlane';
      setMode('prePitlane');
      previousSpeedRef.current = speed;
      highestSpeedRef.current = speed;
      consecutiveDecreasingSamplesRef.current = 0;
      brakingStartSpeedRef.current = null;
      setBrakingStartSpeed(null);
      return;
    }

    if (modeRef.current !== 'prePitlane') {
      return;
    }

    const previousSpeed = previousSpeedRef.current;
    const highestSpeed = highestSpeedRef.current;

    if (speed !== null) {
      if (previousSpeed !== null && speed < previousSpeed) {
        consecutiveDecreasingSamplesRef.current += 1;
      } else {
        consecutiveDecreasingSamplesRef.current = 0;
      }

      if (
        highestSpeed !== null &&
        brakingStartSpeedRef.current === null &&
        consecutiveDecreasingSamplesRef.current >= DECREASING_SAMPLES_REQUIRED
      ) {
        brakingStartSpeedRef.current = highestSpeed;
        setBrakingStartSpeed(highestSpeed);
      }

      previousSpeedRef.current = speed;
      if (brakingStartSpeedRef.current === null &&
          (highestSpeed === null || speed > highestSpeed)) {
        highestSpeedRef.current = speed;
      }
    }
  }, [snapshot]);

  if (mode === 'hidden') {
    return null;
  }

  const speed = snapshot.speedMetersPerSecond;
  const limit = snapshot.pitSpeedLimitMetersPerSecond;
  const isInPitsPrePitstop = mode === 'inPitsPrePitstop';
  const isAfterPitstop = mode === 'afterPitstop';
  const range = brakingStartSpeed !== null && limit !== null
    ? brakingStartSpeed - limit
    : null;
  const prePitProgress = speed !== null && limit !== null && range !== null && range > 0
    ? Math.min(100, Math.max(0, ((speed - limit) / range) * 100))
    : 0;
  const inPitsProgress = speed !== null && limit !== null
    ? Math.min(
        100,
        Math.max(
          0,
          ((speed * 3.6 - (limit * 3.6 - PIT_SPEED_RANGE_KMH)) /
            (PIT_SPEED_RANGE_KMH * 2)) * 100,
        ),
      )
    : 50;
  const afterPitstopProgress = speed !== null && limit !== null && limit > 0
    ? Math.min(100, Math.max(0, (speed / limit) * 100))
    : 0;
  const progress = isAfterPitstop
    ? afterPitstopProgress
    : isInPitsPrePitstop
      ? inPitsProgress
      : prePitProgress;
  const hasCalibration = isAfterPitstop || isInPitsPrePitstop || (range !== null && range > 0);
  const isUnderLimit = speed !== null && limit !== null && speed <= limit;
  const greenProgress = Math.min(progress, 50);
  const redProgress = Math.max(0, progress - 50);

  return (
    <div
      className="pit-speed-helper-widget"
      aria-label="Pit Speed Helper"
      data-mode={mode}
      data-calibrated={hasCalibration}
    >
      <div className="pit-speed-helper-bar" aria-hidden="true">
        {isInPitsPrePitstop ? (
          <>
            <div
              className="pit-speed-helper-fill pit-speed-helper-fill-green"
              style={{ height: `${greenProgress}%` }}
            />
            <div
              className="pit-speed-helper-fill pit-speed-helper-fill-red"
              style={{ height: `${redProgress}%` }}
            />
            <div className="pit-speed-helper-limit-line" />
          </>
        ) : (
          <div
            className={`pit-speed-helper-fill${isAfterPitstop ? ' pit-speed-helper-fill-green' : ''}`}
            style={{ height: `${progress}%` }}
          />
        )}
      </div>
      <div className="pit-speed-helper-readout">
        <strong>{hasCalibration ? `${Math.round(progress)}%` : 'BRAKE'}</strong>
        <span className="pit-speed-helper-mode">
          {isAfterPitstop ? 'AFTER PITSTOP' : isInPitsPrePitstop ? 'PIT LIMIT' : 'PRE-PIT'}
        </span>
        <span>
          {speed !== null ? `${(speed * 3.6).toFixed(0)} km/h` : '—'}
        </span>
        <span className="pit-speed-helper-limit">
          {limit !== null ? `LIMIT ${(limit * 3.6).toFixed(0)}` : 'LIMIT —'}
        </span>
        {isUnderLimit && <span className="pit-speed-helper-safe">OK</span>}
      </div>
    </div>
  );
}
