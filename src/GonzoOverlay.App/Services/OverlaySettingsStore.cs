using System.IO;
using System.Text.Json;
using GonzoOverlay.App.Models;

namespace GonzoOverlay.App.Services;

public sealed class OverlaySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GonzoOverlay",
        "settings.json");

    public OverlaySettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<OverlaySettings>(json, JsonOptions)
                    ?? new OverlaySettings();

                // Older settings files did not contain widget layout information.
                if (settings.Widgets is null || settings.Widgets.Count == 0)
                {
                    settings.Widgets = new()
                    {
                        OverlayWidgetSettings.CreateWeatherDefault(),
                        OverlayWidgetSettings.CreatePitSpeedHelperDefault(),
                        OverlayWidgetSettings.CreateStandingsDefault(),
                        OverlayWidgetSettings.CreateRelativeDefault()
                    };
                }

                AddMissingWidget(settings, OverlayWidgetIds.Weather, OverlayWidgetSettings.CreateWeatherDefault);
                AddMissingWidget(settings, OverlayWidgetIds.PitSpeedHelper, OverlayWidgetSettings.CreatePitSpeedHelperDefault);
                AddMissingWidget(settings, OverlayWidgetIds.Standings, OverlayWidgetSettings.CreateStandingsDefault);
                AddMissingWidget(settings, OverlayWidgetIds.Relative, OverlayWidgetSettings.CreateRelativeDefault);

                return settings;
            }
        }
        catch
        {
            // Invalid or inaccessible settings fall back to defaults.
        }

        return new OverlaySettings();
    }

    private static void AddMissingWidget(
        OverlaySettings settings,
        string widgetId,
        Func<OverlayWidgetSettings> createDefault)
    {
        if (settings.Widgets.Any(widget => widget.Id == widgetId))
        {
            return;
        }

        settings.Widgets.Add(createDefault());
    }

    public void Save(OverlaySettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
