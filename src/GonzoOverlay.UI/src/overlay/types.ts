export const widgetIds = {
  weather: 'weather',
} as const;

export type WidgetId = (typeof widgetIds)[keyof typeof widgetIds];

export type WidgetSettings = {
  id: string;
  enabled: boolean;
  left: number;
  top: number;
  width: number;
  height: number;
};

export type OverlayConfig = {
  editMode: boolean;
  widgets: WidgetSettings[];
};

export type WeatherSnapshot = {
  trackWetness: string;
  timestamp: string;
};

export type OverlayMessage =
  | { type: 'overlay.config'; payload: OverlayConfig }
  | { type: 'weather'; payload: WeatherSnapshot }
  | { type: 'connection'; payload: { state: string } }
  | { type: 'telemetry'; payload: unknown };

export type OverlayHostMessage = { type: 'overlay.ready' };
