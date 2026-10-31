import { useEffect, useMemo, useState } from 'react';
import { WidgetSlot } from './widgets/WidgetSlot';
import { widgetRegistry, type WidgetRenderContext } from './widgets/WidgetRegistry';
import type {
  OverlayConfig,
  OverlayHostMessage,
  OverlayMessage,
  TelemetrySnapshot,
  StandingsSnapshot,
  WeatherSnapshot,
  RelativeSnapshot,
  FuelSnapshot,
} from './overlay/types';

const defaultWeather: WeatherSnapshot = {
  trackWetness: 'Unknown',
  timestamp: new Date().toISOString(),
};

const defaultTelemetry: TelemetrySnapshot = {
  speedMetersPerSecond: null,
  speedKmh: null,
  pitSpeedLimitMetersPerSecond: null,
  onPitRoad: null,
  isApproachingPits: null,
  rpm: null,
  gear: null,
  fuelLevelLiters: null,
  timestamp: new Date().toISOString(),
};

const defaultStandings: StandingsSnapshot = {
  sessionType: 'Race',
  entries: [
    { position: 1, number: '12', driver: 'Gonzo Driver', car: 'Porsche 992', lastLapSeconds: 108.302, bestLapSeconds: 108.302, lapsCompleted: 5, lapsStarted: 6, intervalSeconds: null, leaderGapSeconds: null, lapsBehindLeader: 0, lapsBehindAhead: null, safetyRating: 'A 3.42', iRating: 2400 },
    { position: 2, number: '27', driver: 'Demo Racer', car: 'Ferrari 296', lastLapSeconds: 108.511, bestLapSeconds: 108.511, lapsCompleted: 5, lapsStarted: 6, intervalSeconds: 0.842, leaderGapSeconds: 0.842, lapsBehindLeader: 0, lapsBehindAhead: 0, safetyRating: 'B 3.18', iRating: 3100 },
    { position: 3, number: '84', driver: 'Sample Driver', car: 'BMW M4 GT3', lastLapSeconds: 109.006, bestLapSeconds: 109.006, lapsCompleted: 5, lapsStarted: 6, intervalSeconds: 1.203, leaderGapSeconds: 2.045, lapsBehindLeader: 0, lapsBehindAhead: 0, safetyRating: 'A 4.01', iRating: 4800 },
    { position: 4, number: '5', driver: 'Test Competitor', car: 'Mercedes AMG', lastLapSeconds: 109.32, bestLapSeconds: 109.32, lapsCompleted: 5, lapsStarted: 6, intervalSeconds: 0.774, leaderGapSeconds: 2.819, lapsBehindLeader: 0, lapsBehindAhead: 0, safetyRating: 'C 2.77', iRating: 1900 },
  ],
  timestamp: new Date().toISOString(),
};

const defaultRelative: RelativeSnapshot = {
  playerPosition: 4,
  currentLap: 12,
  sessionLapsTotal: 30,
  estimatedRaceLapsTotal: null,
  raceFinished: false,
  sessionTimeRemainingSeconds: 2847,
  sessionTimeTotalSeconds: 3600,
  entries: [
    { position: 1, driver: 'Michael Hinkle', gapToPlayerSeconds: 2.184, isPlayer: false, isOnPitRoad: false, lapRelation: 'ahead' },
    { position: 2, driver: 'Drew Adamson', gapToPlayerSeconds: 1.102, isPlayer: false, isOnPitRoad: true, lapRelation: 'ahead' },
    { position: 3, driver: 'Cisco Scaramuzza', gapToPlayerSeconds: 0.421, isPlayer: false, isOnPitRoad: false, lapRelation: 'same' },
    { position: 4, driver: 'Gonzo Driver', gapToPlayerSeconds: null, isPlayer: true, isOnPitRoad: false, lapRelation: 'same' },
    { position: 5, driver: 'Tyler Hudson', gapToPlayerSeconds: -0.658, isPlayer: false, isOnPitRoad: false, lapRelation: 'same' },
    { position: 6, driver: 'Max Kearney', gapToPlayerSeconds: -1.227, isPlayer: false, isOnPitRoad: true, lapRelation: 'behind' },
    { position: 7, driver: 'Nigel Pattinson', gapToPlayerSeconds: -2.901, isPlayer: false, isOnPitRoad: false, lapRelation: 'behind' },
  ],
  timestamp: new Date().toISOString(),
};

const defaultFuel: FuelSnapshot = {
  sessionType: 'Race',
  currentFuelLiters: 42.6,
  averageConsumptionPerLapLiters: 2.1,
  lapsOfFuelRemaining: 20.29,
  fuelToAddLiters: 0,
  fuelAtFinishLiters: 1.25,
  timestamp: new Date().toISOString(),
};

function postToHost(message: OverlayHostMessage) {
  window.chrome?.webview?.postMessage(message);
}

function App() {
  const [config, setConfig] = useState<OverlayConfig>({
    editMode: false,
    widgets: [],
  });
  const [weather, setWeather] = useState(defaultWeather);
  const [telemetry, setTelemetry] = useState(defaultTelemetry);
  const [standings, setStandings] = useState(defaultStandings);
  const [relative, setRelative] = useState(defaultRelative);
  const [fuel, setFuel] = useState(defaultFuel);

  useEffect(() => {
    const webView = window.chrome?.webview;
    if (!webView) {
      return;
    }

    const handleMessage = (event: MessageEvent<OverlayMessage>) => {
      const message = event.data;

      if (message.type === 'overlay.config') {
        setConfig(message.payload);
      }

      if (message.type === 'weather') {
        setWeather(message.payload);
      }

      if (message.type === 'telemetry') {
        setTelemetry(message.payload);
      }

      if (message.type === 'standings') {
        setStandings(message.payload);
      }

      if (message.type === 'relative') {
        setRelative(message.payload);
      }

      if (message.type === 'fuel') {
        setFuel(message.payload);
      }

      if (message.type === 'connection' && message.payload.state !== 'Connected') {
        setWeather((current) => ({
          ...current,
          trackWetness: 'Unknown',
        }));
        setTelemetry((current) => ({
          ...current,
          onPitRoad: false,
          isApproachingPits: false,
          speedMetersPerSecond: null,
          speedKmh: null,
        }));
        setFuel((current) => ({
          ...current,
          sessionType: 'Unknown',
          currentFuelLiters: null,
          averageConsumptionPerLapLiters: null,
          lapsOfFuelRemaining: null,
          fuelToAddLiters: null,
          fuelAtFinishLiters: null,
        }));
      }
    };

    webView.addEventListener('message', handleMessage);
    postToHost({ type: 'overlay.ready' });
    return () => webView.removeEventListener('message', handleMessage);
  }, []);

  const renderContext = useMemo<WidgetRenderContext>(
    () => ({ weather, telemetry, standings, relative, fuel }),
    [fuel, relative, standings, telemetry, weather],
  );

  return (
    <main className="overlay-shell">
      {config.widgets
        .filter((settings) => settings.enabled)
        .map((settings) => {
          const definition = widgetRegistry.find((widget) => widget.id === settings.id);
          if (!definition) {
            return null;
          }

          return (
            <WidgetSlot
              key={settings.id}
              settings={settings}
            >
              {definition.render(renderContext)}
            </WidgetSlot>
          );
        })}
    </main>
  );
}

export default App;

declare global {
  interface Window {
    chrome?: {
      webview?: {
        addEventListener: (
          type: 'message',
          listener: (event: MessageEvent<OverlayMessage>) => void,
        ) => void;
        removeEventListener: (
          type: 'message',
          listener: (event: MessageEvent<OverlayMessage>) => void,
        ) => void;
        postMessage: (message: OverlayHostMessage) => void;
      };
    };
  }
}
