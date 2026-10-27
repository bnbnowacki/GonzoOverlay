namespace GonzoOverlay.App.Models;

public sealed class OverlaySettings
{
    public List<OverlayWidgetSettings> Widgets { get; set; } = new()
    {
        OverlayWidgetSettings.CreateWeatherDefault(),
        OverlayWidgetSettings.CreatePitSpeedHelperDefault(),
        OverlayWidgetSettings.CreateStandingsDefault(),
        OverlayWidgetSettings.CreateRelativeDefault(),
        OverlayWidgetSettings.CreateFuelCalculatorDefault()
    };
}
