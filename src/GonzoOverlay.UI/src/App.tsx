import { useEffect, useState } from 'react';

type WeatherSnapshot = {
  trackWetness: string;
  timestamp: string;
};

type OverlayMessage =
  | { type: 'weather'; payload: WeatherSnapshot }
  | { type: 'connection'; payload: { state: string } };

function App() {
  const [weather, setWeather] = useState('Unknown');

  useEffect(() => {
    const webView = window.chrome?.webview;
    if (!webView) {
      return;
    }

    const handleMessage = (event: MessageEvent<OverlayMessage>) => {
      if (event.data.type === 'weather') {
        setWeather(event.data.payload.trackWetness);
      }

      if (event.data.type === 'connection' && event.data.payload.state !== 'Connected') {
        setWeather('Unknown');
      }
    };

    webView.addEventListener('message', handleMessage);
    return () => webView.removeEventListener('message', handleMessage);
  }, []);

  return (
    <main className="weather-overlay">
      <span className="weather-label">TRACK</span>
      <strong>{weather}</strong>
    </main>
  );
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
