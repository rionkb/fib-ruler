using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FibRuler.Models;
using FibRuler.Services;

namespace FibRuler;

public partial class SettingsWindow : Window
{
    private readonly MainWindow _overlay;
    private ShortcutTarget? _capturingTarget;
    private bool _updatingUi;

    public SettingsWindow(MainWindow overlay)
    {
        _updatingUi = true;
        InitializeComponent();
        _updatingUi = false;
        _overlay = overlay;
        UpdateUi();
    }

    private void ChangeShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        string? shortcutType = (sender as Button)?.Tag as string;
        _capturingTarget = shortcutType switch
        {
            "Draw" => ShortcutTarget.Draw,
            "Delete" => ShortcutTarget.Delete,
            _ => null
        };
        if (_capturingTarget is null)
        {
            return;
        }
        SetShortcutCaptureUi();
        HintText.Text = "Press a shortcut.";
        Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturingTarget is not ShortcutTarget target)
        {
            return;
        }

        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!ShortcutValidator.TryCreate(key, Keyboard.Modifiers, out ToggleShortcutSettings? shortcut, out string error)
            || shortcut is null)
        {
            HintText.Text = error;
            return;
        }

        bool changed = target switch
        {
            ShortcutTarget.Draw => _overlay.TryChangeEditShortcuts(shortcut, _overlay.DeleteShortcutSettings, out error),
            ShortcutTarget.Delete => _overlay.TryChangeEditShortcuts(_overlay.DrawShortcutSettings, shortcut, out error),
            _ => false
        };
        if (!changed)
        {
            HintText.Text = error;
            return;
        }

        _capturingTarget = null;
        SetShortcutCaptureUi();
        UpdateUi();
    }

    private void ColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingUi)
        {
            ApplyAppearance();
        }
    }

    private void BandOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updatingUi && IsLoaded)
        {
            ApplyAppearance();
        }

        if (BandOpacityText is not null)
        {
            BandOpacityText.Text = $"{BandOpacitySlider.Value:P0}";
        }
    }

    private void ResetColorsButton_Click(object sender, RoutedEventArgs e)
    {
        _overlay.ResetAppearance();
        UpdateUi();
        HintText.Text = "TradingView colors restored.";
    }

    private void ApplyAppearance()
    {
        ColorSettings colors = _overlay.CurrentColors;
        colors.BaseLevelColor = BaseColorTextBox.Text;
        colors.RedColor = RedColorTextBox.Text;
        colors.GreenColor = GreenColorTextBox.Text;
        colors.OrangeColor = OrangeColorTextBox.Text;
        colors.CyanColor = CyanColorTextBox.Text;
        colors.BlueColor = BlueColorTextBox.Text;
        colors.PurpleColor = PurpleColorTextBox.Text;
        colors.PinkColor = PinkColorTextBox.Text;
        if (!_overlay.TryApplyAppearance(colors, BandOpacitySlider.Value, out string error))
        {
            HintText.Text = error;
        }
        else if (_capturingTarget is null)
        {
            HintText.Text = string.Empty;
        }
    }

    private void UpdateUi()
    {
        _updatingUi = true;
        try
        {
            CurrentDrawShortcutText.Text = _overlay.DrawShortcutDisplay;
            CurrentDeleteShortcutText.Text = _overlay.DeleteShortcutDisplay;
            ColorSettings colors = _overlay.CurrentColors;
            BaseColorTextBox.Text = colors.BaseLevelColor;
            RedColorTextBox.Text = colors.RedColor;
            GreenColorTextBox.Text = colors.GreenColor;
            OrangeColorTextBox.Text = colors.OrangeColor;
            CyanColorTextBox.Text = colors.CyanColor;
            BlueColorTextBox.Text = colors.BlueColor;
            PurpleColorTextBox.Text = colors.PurpleColor;
            PinkColorTextBox.Text = colors.PinkColor;
            BandOpacitySlider.Value = _overlay.CurrentBandOpacity;
            BandOpacityText.Text = $"{_overlay.CurrentBandOpacity:P0}";
        }
        finally
        {
            _updatingUi = false;
        }

        if (_capturingTarget is null)
        {
            HintText.Text = "F1-F24 alone, or a key with Ctrl, Alt, or Shift.";
        }
    }

    private void SetShortcutCaptureUi()
    {
        bool capturing = _capturingTarget is not null;
        ChangeDrawShortcutButton.IsEnabled = !capturing;
        ChangeDeleteShortcutButton.IsEnabled = !capturing;
    }

    private enum ShortcutTarget
    {
        Draw,
        Delete
    }
}
