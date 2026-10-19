import { useEffect, useMemo, useState } from 'react';
import { WidgetSlot } from './widgets/WidgetSlot';
import { widgetRegistry, type WidgetRenderContext } from './widgets/WidgetRegistry';
import type {
  OverlayConfig,
  OverlayHostMessage,
  OverlayMessage,
  WeatherSnapshot,
} from './overlay/types';

const defaultWeather: WeatherSnapshot = {
  trackWetness: 'Unknown',
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

      if (message.type === 'connection' && message.payload.state !== 'Connected') {
        setWeather((current) => ({
          ...current,
          trackWetness: 'Unknown',
        }));
      }
    };

    webView.addEventListener('message', handleMessage);
    postToHost({ type: 'overlay.ready' });
    return () => webView.removeEventListener('message', handleMessage);
  }, []);

  const renderContext = useMemo<WidgetRenderContext>(() => ({ weather }), [weather]);

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
