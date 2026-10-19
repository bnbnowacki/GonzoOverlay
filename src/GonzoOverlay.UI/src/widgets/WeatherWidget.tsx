import type { WeatherSnapshot } from '../overlay/types';

type WeatherWidgetProps = {
  snapshot: WeatherSnapshot;
};

export function WeatherWidget({ snapshot }: WeatherWidgetProps) {
  return (
    <div className="weather-widget">
      <span className="weather-label">TRACK</span>
      <strong>{snapshot.trackWetness}</strong>
    </div>
  );
}
