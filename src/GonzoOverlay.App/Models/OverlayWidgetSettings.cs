namespace GonzoOverlay.App.Models;

public static class OverlayWidgetIds
{
    public const string Weather = "weather";

    public const string PitSpeedHelper = "pitSpeedHelper";

    public const string Standings = "standings";
}

public sealed class OverlayWidgetSettings
{
    public string Id { get; set; } = OverlayWidgetIds.Weather;

    public bool Enabled { get; set; } = true;

    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; } = 240;

    public double Height { get; set; } = 88;

    public static OverlayWidgetSettings CreateWeatherDefault() => new()
    {
        Id = OverlayWidgetIds.Weather,
        Enabled = true,
        Left = 24,
        Top = 24,
        Width = 240,
        Height = 88
    };

    public static OverlayWidgetSettings CreatePitSpeedHelperDefault() => new()
    {
        Id = OverlayWidgetIds.PitSpeedHelper,
        Enabled = false,
        Left = 288,
        Top = 24,
        Width = 240,
        Height = 88
    };

    public static OverlayWidgetSettings CreateStandingsDefault() => new()
    {
        Id = OverlayWidgetIds.Standings,
        Enabled = true,
        Left = 24,
        Top = 130,
        Width = 760,
        Height = 230
    };
}
