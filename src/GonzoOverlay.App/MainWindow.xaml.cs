using System.Windows;
using GonzoOverlay.App.Models;
using GonzoOverlay.App.Services;

namespace GonzoOverlay.App;

public partial class MainWindow : Window
{
    private readonly OverlaySettingsStore _settingsStore = new();
    private readonly Queue<string> _debugLogLines = new();
    private OverlaySettings _currentSettings = new();
    private OverlayWindow? _overlayWindow;
    private DebugLogWindow? _debugLogWindow;

    private const int MaxDebugLogLines = 20000;

    public MainWindow()
    {
        InitializeComponent();
        _currentSettings = _settingsStore.Load();
        LoadSettings(_currentSettings);
        UpdateStatus("Gotowy. Overlay uruchomi się na monitorze konfiguratora.");
    }

    private void StartOverlay_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings))
        {
            return;
        }

        _settingsStore.Save(settings);
        StopOverlay();

        var overlay = new OverlayWindow(settings)
        {
            Owner = this
        };
        overlay.SettingsChanged += OverlayWindow_OnSettingsChanged;
        overlay.DebugLogUpdated += OverlayWindow_OnDebugLogUpdated;
        overlay.Closed += OverlayWindow_OnClosed;

        _overlayWindow = overlay;
        overlay.Show();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        EditButton.IsEnabled = true;
        WeatherWidgetCheckBox.IsEnabled = false;
        PitSpeedHelperWidgetCheckBox.IsEnabled = false;
        StandingsWidgetCheckBox.IsEnabled = false;
        RelativeWidgetCheckBox.IsEnabled = false;
        FuelCalculatorWidgetCheckBox.IsEnabled = false;
        EditButton.Content = "Edit mode";
        UpdateStatus("Overlay aktywny na monitorze konfiguratora.");
    }

    private void StopOverlay_OnClick(object sender, RoutedEventArgs e)
    {
        StopOverlay();
    }

    private void ToggleDebugLog_OnClick(object sender, RoutedEventArgs e)
    {
        if (_debugLogWindow is { IsVisible: true })
        {
            _debugLogWindow.Activate();
            return;
        }

        var debugLogWindow = new DebugLogWindow
        {
            Owner = this
        };
        debugLogWindow.ClearRequested += DebugLogWindow_OnClearRequested;
        debugLogWindow.Closed += DebugLogWindow_OnClosed;
        _debugLogWindow = debugLogWindow;
        debugLogWindow.SetLogText(GetDebugLogText());
        debugLogWindow.Show();
    }

    private void ToggleEditMode_OnClick(object sender, RoutedEventArgs e)
    {
        if (_overlayWindow is null)
        {
            return;
        }

        var editModeEnabled = !_overlayWindow.IsEditMode;

        if (editModeEnabled)
        {
            Topmost = true;
            Activate();
        }

        _overlayWindow.SetEditMode(editModeEnabled);

        if (editModeEnabled)
        {
            EditButton.Content = "Exit edit mode";
            UpdateStatus("Edit mode: przesuwaj widgety po całym monitorze i zmieniaj ich rozmiar.");
        }
        else
        {
            Topmost = false;
            EditButton.Content = "Edit mode";
            UpdateStatus("Ustawienia widgetów zapisane.");
        }
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        StopOverlay();
        _debugLogWindow?.Close();
    }

    private void OverlayWindow_OnSettingsChanged(object? sender, EventArgs e)
    {
        if (sender is OverlayWindow overlay && ReferenceEquals(overlay, _overlayWindow))
        {
            _settingsStore.Save(overlay.Settings);
        }
    }

    private void OverlayWindow_OnClosed(object? sender, EventArgs e)
    {
        if (sender is not OverlayWindow overlay || !ReferenceEquals(overlay, _overlayWindow))
        {
            return;
        }

        overlay.SettingsChanged -= OverlayWindow_OnSettingsChanged;
        overlay.DebugLogUpdated -= OverlayWindow_OnDebugLogUpdated;
        SaveCurrentOverlaySettings();
        _overlayWindow = null;
        Topmost = false;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        EditButton.IsEnabled = false;
        WeatherWidgetCheckBox.IsEnabled = true;
        PitSpeedHelperWidgetCheckBox.IsEnabled = true;
        StandingsWidgetCheckBox.IsEnabled = true;
        RelativeWidgetCheckBox.IsEnabled = true;
        FuelCalculatorWidgetCheckBox.IsEnabled = true;
        EditButton.Content = "Edit mode";
        UpdateStatus("Overlay zatrzymany.");
    }

    private void StopOverlay()
    {
        if (_overlayWindow is null)
        {
            return;
        }

        var overlay = _overlayWindow;
        overlay.SetEditMode(false);
        SaveCurrentOverlaySettings();
        overlay.SettingsChanged -= OverlayWindow_OnSettingsChanged;
        overlay.DebugLogUpdated -= OverlayWindow_OnDebugLogUpdated;
        _overlayWindow = null;
        overlay.Close();

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        EditButton.IsEnabled = false;
        WeatherWidgetCheckBox.IsEnabled = true;
        PitSpeedHelperWidgetCheckBox.IsEnabled = true;
        StandingsWidgetCheckBox.IsEnabled = true;
        RelativeWidgetCheckBox.IsEnabled = true;
        FuelCalculatorWidgetCheckBox.IsEnabled = true;
        EditButton.Content = "Edit mode";
        Topmost = false;
        UpdateStatus("Overlay zatrzymany.");
    }

    private void SaveCurrentOverlaySettings()
    {
        if (_overlayWindow is null)
        {
            return;
        }

        var settings = _overlayWindow.Settings;
        _settingsStore.Save(settings);
        LoadSettings(settings);
    }

    private void LoadSettings(OverlaySettings settings)
    {
        _currentSettings = settings;
        settings.Widgets ??= new()
        {
            OverlayWidgetSettings.CreateWeatherDefault()
        };

        WeatherWidgetCheckBox.IsChecked = settings.Widgets.Any(
            widget => widget.Id == OverlayWidgetIds.Weather && widget.Enabled);
        PitSpeedHelperWidgetCheckBox.IsChecked = settings.Widgets.Any(
            widget => widget.Id == OverlayWidgetIds.PitSpeedHelper && widget.Enabled);
        StandingsWidgetCheckBox.IsChecked = settings.Widgets.Any(
            widget => widget.Id == OverlayWidgetIds.Standings && widget.Enabled);
        RelativeWidgetCheckBox.IsChecked = settings.Widgets.Any(
            widget => widget.Id == OverlayWidgetIds.Relative && widget.Enabled);
        FuelCalculatorWidgetCheckBox.IsChecked = settings.Widgets.Any(
            widget => widget.Id == OverlayWidgetIds.FuelCalculator && widget.Enabled);
    }

    private bool TryReadSettings(out OverlaySettings settings)
    {
        var widgets = _currentSettings.Widgets
            .Select(CloneWidgetSettings)
            .ToList();
        var weather = widgets.FirstOrDefault(widget => widget.Id == OverlayWidgetIds.Weather);

        if (weather is null)
        {
            weather = OverlayWidgetSettings.CreateWeatherDefault();
            widgets.Add(weather);
        }

        weather.Enabled = WeatherWidgetCheckBox.IsChecked == true;
        var pitSpeedHelper = widgets.FirstOrDefault(
            widget => widget.Id == OverlayWidgetIds.PitSpeedHelper);

        if (pitSpeedHelper is null)
        {
            pitSpeedHelper = OverlayWidgetSettings.CreatePitSpeedHelperDefault();
            widgets.Add(pitSpeedHelper);
        }

        pitSpeedHelper.Enabled = PitSpeedHelperWidgetCheckBox.IsChecked == true;
        var standings = widgets.FirstOrDefault(
            widget => widget.Id == OverlayWidgetIds.Standings);

        if (standings is null)
        {
            standings = OverlayWidgetSettings.CreateStandingsDefault();
            widgets.Add(standings);
        }

        standings.Enabled = StandingsWidgetCheckBox.IsChecked == true;
        var relative = widgets.FirstOrDefault(
            widget => widget.Id == OverlayWidgetIds.Relative);

        if (relative is null)
        {
            relative = OverlayWidgetSettings.CreateRelativeDefault();
            widgets.Add(relative);
        }

        relative.Enabled = RelativeWidgetCheckBox.IsChecked == true;
        var fuelCalculator = widgets.FirstOrDefault(
            widget => widget.Id == OverlayWidgetIds.FuelCalculator);

        if (fuelCalculator is null)
        {
            fuelCalculator = OverlayWidgetSettings.CreateFuelCalculatorDefault();
            widgets.Add(fuelCalculator);
        }

        fuelCalculator.Enabled = FuelCalculatorWidgetCheckBox.IsChecked == true;
        settings = new OverlaySettings
        {
            Widgets = widgets
        };
        return true;
    }

    private static OverlayWidgetSettings CloneWidgetSettings(OverlayWidgetSettings widget) => new()
    {
        Id = widget.Id,
        Enabled = widget.Enabled,
        Left = widget.Left,
        Top = widget.Top,
        Width = widget.Width,
        Height = widget.Height
    };

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    private void OverlayWindow_OnDebugLogUpdated(string line)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _debugLogLines.Enqueue(line);
            while (_debugLogLines.Count > MaxDebugLogLines)
            {
                _debugLogLines.Dequeue();
            }

            if (_debugLogWindow is { IsVisible: true })
            {
                _debugLogWindow?.SetLogText(GetDebugLogText());
            }
        });
    }

    private string GetDebugLogText()
    {
        return string.Join(Environment.NewLine, _debugLogLines);
    }

    private void DebugLogWindow_OnClearRequested(object? sender, EventArgs e)
    {
        _debugLogLines.Clear();
        _debugLogWindow?.SetLogText(string.Empty);
    }

    private void DebugLogWindow_OnClosed(object? sender, EventArgs e)
    {
        if (sender is not DebugLogWindow debugLogWindow ||
            !ReferenceEquals(debugLogWindow, _debugLogWindow))
        {
            return;
        }

        debugLogWindow.ClearRequested -= DebugLogWindow_OnClearRequested;
        debugLogWindow.Closed -= DebugLogWindow_OnClosed;
        _debugLogWindow = null;
    }
}
