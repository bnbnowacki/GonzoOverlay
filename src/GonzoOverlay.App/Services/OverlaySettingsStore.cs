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
                return JsonSerializer.Deserialize<OverlaySettings>(json, JsonOptions)
                    ?? new OverlaySettings();
            }
        }
        catch
        {
            // Invalid or inaccessible settings fall back to defaults.
        }

        return new OverlaySettings();
    }

    public void Save(OverlaySettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
