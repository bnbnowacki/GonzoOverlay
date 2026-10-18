using System.Globalization;
using System.Windows;
using GonzoOverlay.App.Models;
using GonzoOverlay.App.Services;

namespace GonzoOverlay.App;

public partial class MainWindow : Window
{
    private readonly OverlaySettingsStore _settingsStore = new();
    private OverlayWindow? _overlayWindow;

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings(_settingsStore.Load());
        UpdateStatus("Gotowy. Ustaw parametry i uruchom overlay.");
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
        overlay.Closed += OverlayWindow_OnClosed;

        _overlayWindow = overlay;
        overlay.Show();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        UpdateStatus($"Overlay aktywny: {settings.Width:0} × {settings.Height:0} px, X={settings.Left:0}, Y={settings.Top:0}.");
    }

    private void StopOverlay_OnClick(object sender, RoutedEventArgs e)
    {
        StopOverlay();
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        StopOverlay();
    }

    private void OverlayWindow_OnClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _overlayWindow))
        {
            _overlayWindow = null;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            UpdateStatus("Overlay zatrzymany.");
        }
    }

    private void StopOverlay()
    {
        if (_overlayWindow is null)
        {
            return;
        }

        var overlay = _overlayWindow;
        _overlayWindow = null;
        overlay.Close();

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        UpdateStatus("Overlay zatrzymany.");
    }

    private void LoadSettings(OverlaySettings settings)
    {
        WidthTextBox.Text = Format(settings.Width);
        HeightTextBox.Text = Format(settings.Height);
        LeftTextBox.Text = Format(settings.Left);
        TopTextBox.Text = Format(settings.Top);
    }

    private bool TryReadSettings(out OverlaySettings settings)
    {
        settings = new OverlaySettings();

        if (!TryReadNumber(WidthTextBox, "szerokość", out var width) ||
            !TryReadNumber(HeightTextBox, "wysokość", out var height) ||
            !TryReadNumber(LeftTextBox, "pozycję X", out var left) ||
            !TryReadNumber(TopTextBox, "pozycję Y", out var top))
        {
            return false;
        }

        if (width < 120 || height < 50)
        {
            MessageBox.Show(
                "Minimalny rozmiar overlaya to 120 × 50 px.",
                "Nieprawidłowy rozmiar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        settings = new OverlaySettings
        {
            Width = width,
            Height = height,
            Left = left,
            Top = top
        };

        return true;
    }

    private static bool TryReadNumber(System.Windows.Controls.TextBox textBox, string fieldName, out double value)
    {
        if (double.TryParse(
                textBox.Text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out value))
        {
            return true;
        }

        MessageBox.Show(
            $"Wpisz poprawną wartość dla pola: {fieldName}.",
            "Nieprawidłowa wartość",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        textBox.Focus();
        textBox.SelectAll();
        return false;
    }

    private static string Format(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }
}
