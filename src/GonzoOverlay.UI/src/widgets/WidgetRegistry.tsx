import type { ReactNode } from 'react';
import { WeatherWidget } from './WeatherWidget';
import { PitSpeedHelperWidget } from './PitSpeedHelperWidget';
import type { TelemetrySnapshot, WeatherSnapshot, WidgetId } from '../overlay/types';

export type WidgetRenderContext = {
  weather: WeatherSnapshot;
  telemetry: TelemetrySnapshot;
};

type WidgetDefinition = {
  id: WidgetId;
  render: (context: WidgetRenderContext) => ReactNode;
};

export const widgetRegistry: WidgetDefinition[] = [
  {
    id: 'weather',
    render: ({ weather }) => <WeatherWidget snapshot={weather} />,
  },
  {
    id: 'pitSpeedHelper',
    render: ({ telemetry }) => <PitSpeedHelperWidget snapshot={telemetry} />,
  },
];
