using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FibRuler.Models;
using FibRuler.Services;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace FibRuler;

public partial class SettingsWindow : Window
{
    private readonly MainWindow _overlay;
    private readonly Dictionary<string, TextBox> _colorTextBoxes;
    private readonly Dictionary<string, Button> _colorButtons;
    private ShortcutTarget? _capturingTarget;
    private bool _updatingUi;

    public SettingsWindow(MainWindow overlay)
    {
        _updatingUi = true;
        InitializeComponent();
        _updatingUi = false;
        _colorTextBoxes = new Dictionary<string, TextBox>
        {
            ["Base"] = BaseColorTextBox,
            ["Red"] = RedColorTextBox,
            ["Green"] = GreenColorTextBox,
            ["Orange"] = OrangeColorTextBox,
            ["Cyan"] = CyanColorTextBox,
            ["Blue"] = BlueColorTextBox,
            ["Purple"] = PurpleColorTextBox,
            ["Pink"] = PinkColorTextBox
        };
        _colorButtons = new Dictionary<string, Button>
        {
            ["Base"] = BaseColorButton,
            ["Red"] = RedColorButton,
            ["Green"] = GreenColorButton,
            ["Orange"] = OrangeColorButton,
            ["Cyan"] = CyanColorButton,
            ["Blue"] = BlueColorButton,
            ["Purple"] = PurpleColorButton,
            ["Pink"] = PinkColorButton
        };
        _overlay = overlay;
        UpdateUi();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        _capturingTarget = null;
        SetShortcutCaptureUi();
        Close();
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

    private void ColorSampleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string colorKey })
        {
            return;
        }

        string currentValue = _overlay.CurrentColors.GetColor(colorKey);
        if (!ColorSettings.TryNormalizeHex(currentValue, out string normalized))
        {
            return;
        }

        using Forms.ColorDialog dialog = new()
        {
            Color = Drawing.ColorTranslator.FromHtml(normalized),
            FullOpen = true,
            AnyColor = true
        };
        if (dialog.ShowDialog(new WindowHandle(new WindowInteropHelper(this).Handle)) != Forms.DialogResult.OK)
        {
            return;
        }

        string selectedColor = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        _updatingUi = true;
        try
        {
            _colorTextBoxes[colorKey].Text = selectedColor;
        }
        finally
        {
            _updatingUi = false;
        }

        ApplyAppearance();
        UpdateUi();
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
        foreach (string key in ColorSettings.ColorKeys)
        {
            colors.SetColor(key, _colorTextBoxes[key].Text);
        }

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
            foreach (string key in ColorSettings.ColorKeys)
            {
                _colorTextBoxes[key].Text = colors.GetColor(key);
            }

            BandOpacitySlider.Value = _overlay.CurrentBandOpacity;
            BandOpacityText.Text = $"{_overlay.CurrentBandOpacity:P0}";
            UpdateColorSwatches(colors);
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

    private void UpdateColorSwatches(ColorSettings colors)
    {
        foreach (string key in ColorSettings.ColorKeys)
        {
            SetColorSwatch(_colorButtons[key], colors.GetColor(key));
        }
    }

    private static void SetColorSwatch(Button button, string value)
    {
        button.Background = ColorSettings.TryNormalizeHex(value, out string normalized)
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(normalized))
            : Brushes.Transparent;
    }

    private sealed class WindowHandle : Forms.IWin32Window
    {
        public WindowHandle(IntPtr handle) => Handle = handle;

        public IntPtr Handle { get; }
    }

    protected override void OnClosed(EventArgs e)
    {
        _capturingTarget = null;
        base.OnClosed(e);
    }

    private enum ShortcutTarget
    {
        Draw,
        Delete
    }
}
