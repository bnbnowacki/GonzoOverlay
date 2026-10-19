import type { ReactNode } from 'react';
import { WeatherWidget } from './WeatherWidget';
import type { WeatherSnapshot, WidgetId } from '../overlay/types';

export type WidgetRenderContext = {
  weather: WeatherSnapshot;
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
];
