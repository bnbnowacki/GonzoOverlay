export const widgetIds = {
  weather: 'weather',
  pitSpeedHelper: 'pitSpeedHelper',
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

export type TelemetrySnapshot = {
  speedMetersPerSecond: number | null;
  speedKmh: number | null;
  pitSpeedLimitMetersPerSecond: number | null;
  onPitRoad: boolean | null;
  isApproachingPits: boolean | null;
  rpm: number | null;
  gear: number | null;
  fuelLevelLiters: number | null;
  timestamp: string;
};

export type OverlayMessage =
  | { type: 'overlay.config'; payload: OverlayConfig }
  | { type: 'weather'; payload: WeatherSnapshot }
  | { type: 'connection'; payload: { state: string } }
  | { type: 'telemetry'; payload: TelemetrySnapshot };

export type OverlayHostMessage = { type: 'overlay.ready' };
