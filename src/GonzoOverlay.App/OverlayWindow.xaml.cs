using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly CancellationTokenSource _telemetryCancellation = new();
    private readonly IracingTelemetryProvider _telemetryProvider;
    private readonly OverlaySettings _settings;
    private readonly List<WidgetEditState> _editWidgets = new();
    private Task? _telemetryTask;
    private WidgetEditState? _activeEditWidget;
    private bool _webViewReady;
    private bool _pageReady;
    private bool _isEditMode;
    private bool _isResizingWidget;
    private Point _interactionStartPoint;
    private double _interactionStartLeft;
    private double _interactionStartTop;
    private double _interactionStartWidth;
    private double _interactionStartHeight;

    public OverlayWindow(OverlaySettings settings)
    {
        InitializeComponent();

        _settings = settings;
        _settings.Widgets ??= new();

        _telemetryProvider = new IracingTelemetryProvider(
            NullLogger<IracingTelemetryProvider>.Instance);
        _telemetryProvider.TelemetryUpdated += OnTelemetryUpdated;
        _telemetryProvider.WeatherUpdated += OnWeatherUpdated;
        _telemetryProvider.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public OverlaySettings Settings => _settings;

    public bool IsEditMode => _isEditMode;

    public event EventHandler? SettingsChanged;

    public void SetEditMode(bool enabled)
    {
        if (enabled == _isEditMode)
        {
            return;
        }

        if (enabled)
        {
            EnterEditMode();
        }
        else
        {
            ExitEditMode();
        }
    }

    private async void OverlayWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyOwnerMonitorBounds();
        await InitializeWebViewAsync();
        _telemetryTask = _telemetryProvider.StartAsync(_telemetryCancellation.Token);
    }

    private async void OverlayWindow_OnClosed(object? sender, EventArgs e)
    {
        if (_isEditMode)
        {
            ExitEditMode();
        }

        _telemetryCancellation.Cancel();

        if (_telemetryTask is not null)
        {
            await _telemetryTask;
        }

        if (OverlayWebView.CoreWebView2 is not null)
        {
            OverlayWebView.CoreWebView2.WebMessageReceived -= OverlayWebView_OnWebMessageReceived;
        }

        _telemetryProvider.TelemetryUpdated -= OnTelemetryUpdated;
        _telemetryProvider.WeatherUpdated -= OnWeatherUpdated;
        _telemetryProvider.ConnectionStateChanged -= OnConnectionStateChanged;
        await _telemetryProvider.DisposeAsync();
        _telemetryCancellation.Dispose();
    }

    private void ApplyOwnerMonitorBounds()
    {
        var ownerHandle = Owner is null
            ? IntPtr.Zero
            : new WindowInteropHelper(Owner).Handle;
        var windowHandle = new WindowInteropHelper(this).Handle;
        var sourceHandle = ownerHandle != IntPtr.Zero ? ownerHandle : windowHandle;
        var monitorHandle = MonitorFromWindow(sourceHandle, MonitorDefaultToNearest);

        if (monitorHandle == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            return;
        }

        var monitorRect = monitorInfo.Monitor;
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        var topLeft = transform.Transform(new Point(monitorRect.Left, monitorRect.Top));
        var bottomRight = transform.Transform(new Point(monitorRect.Right, monitorRect.Bottom));

        WindowState = WindowState.Normal;
        Left = topLeft.X;
        Top = topLeft.Y;
        Width = bottomRight.X - topLeft.X;
        Height = bottomRight.Y - topLeft.Y;
    }

    private async Task InitializeWebViewAsync()
    {
        await OverlayWebView.EnsureCoreWebView2Async();
        OverlayWebView.CoreWebView2.WebMessageReceived += OverlayWebView_OnWebMessageReceived;
        OverlayWebView.DefaultBackgroundColor =
            System.Drawing.Color.FromArgb(0, 0, 0, 0);
        _webViewReady = true;

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
                  <body style="margin:0;background:transparent;color:white;font-family:Segoe UI;padding:24px;overflow:hidden">
                    <h2>GonzoOverlay</h2>
                    <p>Frontend React nie zostaĹ‚ jeszcze zbudowany.</p>
                    <p>Uruchom <code>npm install</code> oraz <code>npm run build</code> w src/GonzoOverlay.UI.</p>
                  </body>
                </html>
                """);
        }
    }

    private void OverlayWebView_OnWebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            if (typeElement.GetString() == "overlay.ready")
            {
                _pageReady = true;
                SendOverlayConfig();
            }
        }
        catch (JsonException)
        {
            // Ignore malformed messages from the embedded page.
        }
    }

    private void EnterEditMode()
    {
        _isEditMode = true;
        _activeEditWidget = null;
        _editWidgets.Clear();

        OverlayWebView.IsHitTestVisible = false;
        OverlayWebView.Visibility = Visibility.Hidden;
        EditSurface.Visibility = Visibility.Visible;
        OverlayBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 157, 255));
        OverlayBorder.BorderThickness = new Thickness(3);
        OverlayBorder.CornerRadius = new CornerRadius(12);

        foreach (var widget in _settings.Widgets.Where(widget => widget.Enabled))
        {
            var editSettings = CloneWidgetSettings(widget);
            var state = CreateWidgetEditor(editSettings);
            _editWidgets.Add(state);
            EditSurface.Children.Add(state.View);
            UpdateWidgetEditorVisual(state);
        }

        EditModeHint.Visibility = _editWidgets.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ExitEditMode()
    {
        CommitEditLayout();
        _isEditMode = false;
        _activeEditWidget = null;

        if (EditSurface.IsMouseCaptured)
        {
            EditSurface.ReleaseMouseCapture();
        }

        _editWidgets.Clear();
        EditSurface.Children.Clear();
        EditSurface.Visibility = Visibility.Collapsed;
        EditModeHint.Visibility = Visibility.Collapsed;
        OverlayBorder.BorderBrush = Brushes.Transparent;
        OverlayBorder.BorderThickness = new Thickness(0);
        OverlayBorder.CornerRadius = new CornerRadius(0);
        OverlayWebView.Visibility = Visibility.Visible;
        OverlayWebView.IsHitTestVisible = true;

        SendOverlayConfig();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private WidgetEditState CreateWidgetEditor(OverlayWidgetSettings settings)
    {
        var label = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            IsHitTestVisible = false
        };

        var resizeGrip = new Border
        {
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 2, 2),
            Background = new SolidColorBrush(Color.FromArgb(220, 22, 140, 255)),
            CornerRadius = new CornerRadius(4),
            Cursor = Cursors.SizeNWSE,
            ToolTip = "Zmień rozmiar widgetu"
        };

        var content = new Grid();
        content.Children.Add(label);
        content.Children.Add(resizeGrip);

        var view = new Border
        {
            MinWidth = 80,
            MinHeight = 40,
            Background = new SolidColorBrush(Color.FromArgb(210, 11, 42, 74)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 157, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Cursor = Cursors.SizeAll,
            Child = content
        };

        var state = new WidgetEditState(settings, view, label);
        view.MouseLeftButtonDown += (_, args) => BeginWidgetInteraction(state, false, args);
        resizeGrip.MouseLeftButtonDown += (_, args) => BeginWidgetInteraction(state, true, args);
        return state;
    }

    private void BeginWidgetInteraction(
        WidgetEditState widget,
        bool resizing,
        MouseButtonEventArgs e)
    {
        if (!_isEditMode)
        {
            return;
        }

        _activeEditWidget = widget;
        _isResizingWidget = resizing;
        _interactionStartPoint = e.GetPosition(EditSurface);
        _interactionStartLeft = widget.Settings.Left;
        _interactionStartTop = widget.Settings.Top;
        _interactionStartWidth = widget.Settings.Width;
        _interactionStartHeight = widget.Settings.Height;
        EditSurface.CaptureMouse();
        e.Handled = true;
    }

    private void EditSurface_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_activeEditWidget is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPoint = e.GetPosition(EditSurface);
        var deltaX = currentPoint.X - _interactionStartPoint.X;
        var deltaY = currentPoint.Y - _interactionStartPoint.Y;
        var widget = _activeEditWidget.Settings;

        if (_isResizingWidget)
        {
            var maxWidth = Math.Max(80, EditSurface.ActualWidth - _interactionStartLeft);
            var maxHeight = Math.Max(40, EditSurface.ActualHeight - _interactionStartTop);
            widget.Width = Math.Clamp(_interactionStartWidth + deltaX, 80, maxWidth);
            widget.Height = Math.Clamp(_interactionStartHeight + deltaY, 40, maxHeight);
        }
        else
        {
            widget.Left = Math.Clamp(
                _interactionStartLeft + deltaX,
                0,
                Math.Max(0, EditSurface.ActualWidth - widget.Width));
            widget.Top = Math.Clamp(
                _interactionStartTop + deltaY,
                0,
                Math.Max(0, EditSurface.ActualHeight - widget.Height));
        }

        UpdateWidgetEditorVisual(_activeEditWidget);
        e.Handled = true;
    }

    private void EditSurface_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_activeEditWidget is null)
        {
            return;
        }

        _activeEditWidget = null;
        _isResizingWidget = false;

        if (EditSurface.IsMouseCaptured)
        {
            EditSurface.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    private void UpdateWidgetEditorVisual(WidgetEditState widget)
    {
        Canvas.SetLeft(widget.View, widget.Settings.Left);
        Canvas.SetTop(widget.View, widget.Settings.Top);
        widget.View.Width = widget.Settings.Width;
        widget.View.Height = widget.Settings.Height;
        widget.Label.Text = $"{GetWidgetTitle(widget.Settings.Id)}\n{widget.Settings.Width:0} × {widget.Settings.Height:0}";
    }

    private void CommitEditLayout()
    {
        foreach (var editWidget in _editWidgets)
        {
            var current = _settings.Widgets.FirstOrDefault(
                widget => widget.Id == editWidget.Settings.Id);
            if (current is null)
            {
                continue;
            }

            current.Left = editWidget.Settings.Left;
            current.Top = editWidget.Settings.Top;
            current.Width = editWidget.Settings.Width;
            current.Height = editWidget.Settings.Height;
        }
    }

    private void SendOverlayConfig()
    {
        PostMessage("overlay.config", new
        {
            editMode = _isEditMode,
            widgets = _settings.Widgets
        });
    }

    private void OnTelemetryUpdated(TelemetrySnapshot snapshot)
    {
        PostMessage("telemetry", snapshot);
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
        if (!_webViewReady || !_pageReady)
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

    private static OverlayWidgetSettings CloneWidgetSettings(OverlayWidgetSettings widget) => new()
    {
        Id = widget.Id,
        Enabled = widget.Enabled,
        Left = widget.Left,
        Top = widget.Top,
        Width = widget.Width,
        Height = widget.Height
    };

    private static string GetWidgetTitle(string id) => id switch
    {
        OverlayWidgetIds.Weather => "Pogoda toru",
        OverlayWidgetIds.PitSpeedHelper => "Pit Speed Helper",
        _ => id
    };

    private sealed class WidgetEditState
    {
        public WidgetEditState(
            OverlayWidgetSettings settings,
            Border view,
            TextBlock label)
        {
            Settings = settings;
            View = view;
            Label = label;
        }

        public OverlayWidgetSettings Settings { get; }

        public Border View { get; }

        public TextBlock Label { get; }
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
