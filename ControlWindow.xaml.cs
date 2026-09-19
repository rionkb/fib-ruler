using System.Windows;
using System.Windows.Media;
using FibRuler.Services;

namespace FibRuler;

public partial class ControlWindow : Window
{
    private readonly MainWindow _overlay;

    public ControlWindow(MainWindow overlay)
    {
        InitializeComponent();
        _overlay = overlay;
        _overlay.ToolEnabledChanged += Overlay_ToolEnabledChanged;
        _overlay.HotkeyRegistrationFailed += Overlay_HotkeyRegistrationFailed;
        UpdateUi();
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e) => _overlay.ToggleToolEnabled();

    private void Window_Loaded(object sender, RoutedEventArgs e) => UpdateUi();

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsWindow dialog = new(_overlay) { Owner = this };
        dialog.ShowDialog();
        UpdateUi();
    }

    private void Overlay_ToolEnabledChanged(object? sender, EventArgs e) => UpdateUi();

    private void Overlay_HotkeyRegistrationFailed(object? sender, HotkeyRegistrationFailedEventArgs e)
    {
        HotkeyErrorText.Text = e.Message;
        HotkeyErrorText.Visibility = Visibility.Visible;
    }

    private void UpdateUi()
    {
        bool enabled = _overlay.IsToolEnabled;
        StatusText.Text = enabled ? "Overlay: Enabled" : "Overlay: Disabled";
        StatusText.Foreground = enabled
            ? new SolidColorBrush(Color.FromRgb(80, 200, 120))
            : new SolidColorBrush(Color.FromRgb(174, 180, 192));
        ToggleButton.Content = enabled ? "Disable Overlay" : "Enable Overlay";
        DrawShortcutText.Text = _overlay.DrawShortcutDisplay;
        DeleteShortcutText.Text = _overlay.DeleteShortcutDisplay;
    }

    protected override void OnClosed(EventArgs e)
    {
        _overlay.ToolEnabledChanged -= Overlay_ToolEnabledChanged;
        _overlay.HotkeyRegistrationFailed -= Overlay_HotkeyRegistrationFailed;
        if (_overlay.IsVisible)
        {
            _overlay.Close();
        }

        base.OnClosed(e);
    }
}
