export const widgetIds = {
  weather: 'weather',
  pitSpeedHelper: 'pitSpeedHelper',
  standings: 'standings',
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

export type StandingEntry = {
  position: number;
  number: string;
  driver: string;
  car: string;
  lastLapSeconds: number | null;
  bestLapSeconds: number | null;
  lapsCompleted: number | null;
  lapsStarted: number | null;
  intervalSeconds: number | null;
  leaderGapSeconds: number | null;
  safetyRating: string;
  iRating: number | null;
};

export type StandingsSnapshot = {
  sessionType: string;
  entries: StandingEntry[];
  timestamp: string;
};

export type OverlayMessage =
  | { type: 'overlay.config'; payload: OverlayConfig }
  | { type: 'weather'; payload: WeatherSnapshot }
  | { type: 'connection'; payload: { state: string } }
  | { type: 'telemetry'; payload: TelemetrySnapshot }
  | { type: 'standings'; payload: StandingsSnapshot };

export type OverlayHostMessage = { type: 'overlay.ready' };
