using System.Text.Json;
using System.IO;
using System.Windows;
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
    private Task? _telemetryTask;
    private bool _webViewReady;

    public OverlayWindow(OverlaySettings settings)
    {
        InitializeComponent();

        Width = settings.Width;
        Height = settings.Height;
        Left = settings.Left;
        Top = settings.Top;

        _telemetryProvider = new IracingTelemetryProvider(
            NullLogger<IracingTelemetryProvider>.Instance);
        _telemetryProvider.WeatherUpdated += OnWeatherUpdated;
        _telemetryProvider.ConnectionStateChanged += OnConnectionStateChanged;
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
