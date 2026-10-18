using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GonzoOverlay.App.Models;
using GonzoOverlay.Core.Models;
using GonzoOverlay.Infrastructure.iRacing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Web.WebView2.Core;

namespace GonzoOverlay.App;

public partial class OverlayWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly CancellationTokenSource _telemetryCancellation = new();
    private readonly IracingTelemetryProvider _telemetryProvider;
    private readonly OverlaySettings _settings;
    private Task? _telemetryTask;
    private bool _webViewReady;
    private bool _isEditMode;
    private bool _isResizing;
    private double _interactionStartWidth;
    private double _interactionStartHeight;
    private Point _interactionStartScreenPoint;

    private const double ResizeGripSize = 24;

    public OverlayWindow(OverlaySettings settings)
    {
        InitializeComponent();

        _settings = settings;
        Width = settings.Width;
        Height = settings.Height;
        Left = settings.Left;
        Top = settings.Top;

        _telemetryProvider = new IracingTelemetryProvider(
            NullLogger<IracingTelemetryProvider>.Instance);
        _telemetryProvider.WeatherUpdated += OnWeatherUpdated;
        _telemetryProvider.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public OverlaySettings Settings => _settings;

    public bool IsEditMode => _isEditMode;

    public void SetEditMode(bool enabled)
    {
        _isEditMode = enabled;
        _isResizing = false;

        if (!enabled && IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        OverlayWebView.IsHitTestVisible = !enabled;
        OverlayWebView.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        EditHitArea.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        EditModeHint.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        ResizeGrip.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        OverlayBorder.BorderBrush = enabled
            ? new SolidColorBrush(Color.FromRgb(0, 157, 255))
            : new SolidColorBrush(Color.FromRgb(60, 130, 246));
        OverlayBorder.BorderThickness = enabled ? new Thickness(3) : new Thickness(1);
        Cursor = enabled ? Cursors.SizeAll : Cursors.Arrow;
    }

    private async void OverlayWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
        _telemetryTask = _telemetryProvider.StartAsync(_telemetryCancellation.Token);
    }

    private async void OverlayWindow_OnClosed(object? sender, EventArgs e)
    {
        _telemetryCancellation.Cancel();

        if (_telemetryTask is not null)
        {
            await _telemetryTask;
        }

        await _telemetryProvider.DisposeAsync();
        _telemetryCancellation.Dispose();
    }

    private async Task InitializeWebViewAsync()
    {
        await OverlayWebView.EnsureCoreWebView2Async();
        var uiRoot = Path.Combine(AppContext.BaseDirectory, "ui");

        if (Directory.Exists(uiRoot))
        {
            OverlayWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets.local",
                uiRoot,
                CoreWebView2HostResourceAccessKind.Allow);
            OverlayWebView.CoreWebView2.Navigate("https://appassets.local/index.html");
        }
        else
        {
            OverlayWebView.NavigateToString("""
                <html>
                  <body style="background:#111620;color:white;font-family:Segoe UI;padding:24px">
                    <h2>GonzoOverlay</h2>
                    <p>Frontend React nie został jeszcze zbudowany.</p>
                    <p>Uruchom <code>npm install</code> oraz <code>npm run build</code> w src/GonzoOverlay.UI.</p>
                  </body>
                </html>
                """);
        }

        _webViewReady = true;
    }

    private void OnWeatherUpdated(WeatherSnapshot snapshot)
    {
        PostMessage("weather", snapshot);
    }

    private void OnConnectionStateChanged(string state)
    {
        PostMessage("connection", new { state });
    }

    private void OverlayWindow_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isEditMode)
        {
            return;
        }

        if (!IsInResizeGrip(e.GetPosition(this)))
        {
            e.Handled = true;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // The pointer may have been released before DragMove captured it.
            }

            SyncSettingsFromWindow();
            return;
        }

        _interactionStartScreenPoint = e.GetPosition(null);
        _interactionStartWidth = Width;
        _interactionStartHeight = Height;
        _isResizing = true;
        CaptureMouse();
        e.Handled = true;
    }

    private void OverlayWindow_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isEditMode)
        {
            return;
        }

        var currentScreenPoint = e.GetPosition(null);
        var currentPoint = e.GetPosition(this);

        if (_isResizing)
        {
            Width = Math.Max(MinWidth, _interactionStartWidth + currentScreenPoint.X - _interactionStartScreenPoint.X);
            Height = Math.Max(MinHeight, _interactionStartHeight + currentScreenPoint.Y - _interactionStartScreenPoint.Y);
            SyncSettingsFromWindow();
            e.Handled = true;
            return;
        }

        Cursor = IsInResizeGrip(currentPoint) ? Cursors.SizeNWSE : Cursors.SizeAll;
    }

    private void OverlayWindow_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isEditMode)
        {
            return;
        }

        _isResizing = false;

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        SyncSettingsFromWindow();
        e.Handled = true;
    }

    private bool IsInResizeGrip(Point point) =>
        point.X >= ActualWidth - ResizeGripSize &&
        point.Y >= ActualHeight - ResizeGripSize;

    private void SyncSettingsFromWindow()
    {
        _settings.Width = Width;
        _settings.Height = Height;
        _settings.Left = Left;
        _settings.Top = Top;
    }

    private void PostMessage(string type, object payload)
    {
        if (!_webViewReady)
        {
            return;
        }

        Dispatcher.InvokeAsync(() =>
        {
            if (OverlayWebView.CoreWebView2 is null)
            {
                return;
            }

            var message = JsonSerializer.Serialize(new { type, payload }, JsonOptions);
            OverlayWebView.CoreWebView2.PostWebMessageAsJson(message);
        });
    }
}
