import { useEffect, useState } from 'react';

type TelemetrySnapshot = {
  speedKmh: number | null;
  speedMetersPerSecond: number | null;
  rpm: number | null;
  gear: number | null;
  fuelLevelLiters: number | null;
};

type OverlayMessage =
  | { type: 'telemetry'; payload: TelemetrySnapshot }
  | { type: 'connection'; payload: { state: string } };

const demoTelemetry: TelemetrySnapshot = {
  speedKmh: 138.2,
  speedMetersPerSecond: 38.4,
  rpm: 7298,
  gear: 3,
  fuelLevelLiters: 48.55
};

function App() {
  const [telemetry, setTelemetry] = useState<TelemetrySnapshot>(demoTelemetry);
  const [connectionState, setConnectionState] = useState('Demo preview');

  useEffect(() => {
    const webView = window.chrome?.webview;
    if (!webView) {
      return;
    }

    const handleMessage = (event: MessageEvent<OverlayMessage>) => {
      if (event.data.type === 'telemetry') {
        setTelemetry(event.data.payload);
      }

      if (event.data.type === 'connection') {
        setConnectionState(event.data.payload.state);
      }
    };

    webView.addEventListener('message', handleMessage);
    return () => webView.removeEventListener('message', handleMessage);
  }, []);

  return (
    <main className="overlay-shell">
      <header className="overlay-header">
        <div>
          <div className="eyebrow">GONZOOVERLAY</div>
          <h1>Telemetry</h1>
        </div>
        <div className="connection-pill">
          <span className="connection-dot" />
          {connectionState}
        </div>
      </header>

      <section className="telemetry-grid">
        <TelemetryCard label="Prędkość" value={format(telemetry.speedKmh, ' km/h')} accent />
        <TelemetryCard label="Obroty" value={format(telemetry.rpm, ' RPM')} />
        <TelemetryCard label="Bieg" value={telemetry.gear?.toString() ?? '—'} />
        <TelemetryCard label="Paliwo" value={format(telemetry.fuelLevelLiters, ' L')} />
      </section>
    </main>
  );
}

function TelemetryCard({ label, value, accent = false }: { label: string; value: string; accent?: boolean }) {
  return (
    <article className={`telemetry-card${accent ? ' telemetry-card-accent' : ''}`}>
      <span className="telemetry-label">{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function format(value: number | null, suffix: string) {
  return value === null ? '—' : `${value.toFixed(value > 100 ? 0 : 1)}${suffix}`;
}

export default App;

declare global {
  interface Window {
    chrome?: {
      webview?: {
        addEventListener: (type: 'message', listener: (event: MessageEvent<OverlayMessage>) => void) => void;
        removeEventListener: (type: 'message', listener: (event: MessageEvent<OverlayMessage>) => void) => void;
      };
    };
  }
}
