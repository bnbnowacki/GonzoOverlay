import type { ReactNode } from 'react';
import { WeatherWidget } from './WeatherWidget';
import { PitSpeedHelperWidget } from './PitSpeedHelperWidget';
import { StandingsWidget } from './StandingsWidget';
import type { TelemetrySnapshot, WeatherSnapshot, WidgetId } from '../overlay/types';

export type WidgetRenderContext = {
  weather: WeatherSnapshot;
  telemetry: TelemetrySnapshot;
  standings: import('../overlay/types').StandingsSnapshot;
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
  {
    id: 'standings',
    render: ({ standings }) => <StandingsWidget snapshot={standings} />,
  },
];
