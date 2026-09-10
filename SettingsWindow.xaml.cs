using System.Windows;
using System.Windows.Input;
using FibRuler.Models;
using FibRuler.Services;

namespace FibRuler;

public partial class SettingsWindow : Window
{
    private readonly MainWindow _overlay;
    private bool _capturingShortcut;

    public SettingsWindow(MainWindow overlay)
    {
        InitializeComponent();
        _overlay = overlay;
        UpdateUi();
    }

    private void ChangeShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        _capturingShortcut = true;
        ChangeShortcutButton.IsEnabled = false;
        HintText.Text = "Press a shortcut. Function keys may be used alone; other keys require Ctrl or Alt.";
        Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingShortcut)
        {
            return;
        }

        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!ShortcutValidator.TryCreate(key, Keyboard.Modifiers, out ToggleShortcutSettings? shortcut, out string error))
        {
            HintText.Text = error;
            return;
        }

        if (shortcut is null || !_overlay.TryChangeToggleShortcut(shortcut, out error))
        {
            HintText.Text = error;
            return;
        }

        _capturingShortcut = false;
        ChangeShortcutButton.IsEnabled = true;
        UpdateUi();
    }

    private void UpdateUi()
    {
        CurrentShortcutText.Text = _overlay.ToggleShortcutDisplay;
        HintText.Text = _capturingShortcut
            ? "Press a shortcut."
            : "Allowed: F1-F12 alone, or a key with Ctrl and/or Alt.";
    }
}
