using System.Windows;

namespace GonzoOverlay.App;

public partial class DebugLogWindow : Window
{
    public DebugLogWindow()
    {
        InitializeComponent();
    }

    public void SetHeading(string title)
    {
        Title = $"GonzoOverlay — {title}";
        LogHeading.Text = title;
    }

    public event EventHandler? ClearRequested;

    public void SetLogText(string text)
    {
        LogTextBox.Text = text;
        LogTextBox.ScrollToEnd();
    }

    private void CopyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(LogTextBox.Text))
        {
            Clipboard.SetText(LogTextBox.Text);
        }
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        ClearRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
