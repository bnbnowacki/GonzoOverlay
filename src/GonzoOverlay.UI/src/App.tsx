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
    { position: 1, number: '12', driver: 'Gonzo Driver', car: 'Porsche 992', lastLapSeconds: 108.302, bestLapSeconds: 108.302, intervalSeconds: null, leaderGapSeconds: null, safetyRating: 'A 3.42', iRating: 2400 },
    { position: 2, number: '27', driver: 'Demo Racer', car: 'Ferrari 296', lastLapSeconds: 108.511, bestLapSeconds: 108.511, intervalSeconds: 0.842, leaderGapSeconds: 0.842, safetyRating: 'B 3.18', iRating: 3100 },
    { position: 3, number: '84', driver: 'Sample Driver', car: 'BMW M4 GT3', lastLapSeconds: 109.006, bestLapSeconds: 109.006, intervalSeconds: 1.203, leaderGapSeconds: 2.045, safetyRating: 'A 4.01', iRating: 4800 },
    { position: 4, number: '5', driver: 'Test Competitor', car: 'Mercedes AMG', lastLapSeconds: 109.32, bestLapSeconds: 109.32, intervalSeconds: 0.774, leaderGapSeconds: 2.819, safetyRating: 'C 2.77', iRating: 1900 },
  ],
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
      }
    };

    webView.addEventListener('message', handleMessage);
    postToHost({ type: 'overlay.ready' });
    return () => webView.removeEventListener('message', handleMessage);
  }, []);

  const renderContext = useMemo<WidgetRenderContext>(
    () => ({ weather, telemetry, standings }),
    [standings, telemetry, weather],
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
