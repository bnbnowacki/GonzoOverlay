import type { ReactNode } from 'react';
import { WeatherWidget } from './WeatherWidget';
import { PitSpeedHelperWidget } from './PitSpeedHelperWidget';
import { StandingsWidget } from './StandingsWidget';
import { RelativeWidget } from './RelativeWidget';
import type { TelemetrySnapshot, WeatherSnapshot, WidgetId } from '../overlay/types';

export type WidgetRenderContext = {
  weather: WeatherSnapshot;
  telemetry: TelemetrySnapshot;
  standings: import('../overlay/types').StandingsSnapshot;
  relative: import('../overlay/types').RelativeSnapshot;
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
  {
    id: 'relative',
    render: ({ relative }) => <RelativeWidget snapshot={relative} />,
  },
];
