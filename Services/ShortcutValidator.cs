using System.Windows.Input;
using FibRuler.Models;

namespace FibRuler.Services;

public static class ShortcutValidator
{
    public static ToggleShortcutSettings CreateDefault() => new();

    public static bool TryCreate(Key key, ModifierKeys modifiers, out ToggleShortcutSettings? settings, out string error)
    {
        settings = null;
        error = string.Empty;
        if (key is Key.None or Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.Escape or Key.Space or Key.Enter)
        {
            error = "That key cannot be used as a shortcut.";
            return false;
        }

        if (!IsFunctionKey(key) && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == ModifierKeys.None)
        {
            error = "Letters and digits require Ctrl, Alt, or Windows.";
            return false;
        }

        if (KeyInterop.VirtualKeyFromKey(key) == 0)
        {
            error = "That key is not supported.";
            return false;
        }

        settings = ToSettings(key, modifiers);
        return true;
    }

    public static bool TryParse(ToggleShortcutSettings? saved, out ToggleShortcutSettings settings)
    {
        settings = CreateDefault();
        if (saved is null || !Enum.TryParse(saved.Key, true, out Key key)
            || !TryCreate(key, ParseModifiers(saved.Modifiers), out ToggleShortcutSettings? parsed, out _))
        {
            return false;
        }

        settings = parsed!;
        return true;
    }

    public static string Format(ToggleShortcutSettings settings)
    {
        List<string> parts = settings.Modifiers.Select(static item => item switch
        {
            "Control" => "Ctrl",
            "Alt" => "Alt",
            "Shift" => "Shift",
            "Windows" => "Win",
            _ => item
        }).ToList();
        parts.Add(settings.Key);
        return string.Join(" + ", parts);
    }

    public static bool TryGetNative(ToggleShortcutSettings settings, out uint modifiers, out uint virtualKey, out string error)
    {
        error = string.Empty;
        modifiers = 0;
        virtualKey = 0;
        if (!Enum.TryParse(settings.Key, true, out Key key)
            || !TryCreate(key, ParseModifiers(settings.Modifiers), out _, out error)) return false;

        ModifierKeys parsed = ParseModifiers(settings.Modifiers);
        if ((parsed & ModifierKeys.Control) != 0) modifiers |= 0x0002;
        if ((parsed & ModifierKeys.Alt) != 0) modifiers |= 0x0001;
        if ((parsed & ModifierKeys.Shift) != 0) modifiers |= 0x0004;
        if ((parsed & ModifierKeys.Windows) != 0) modifiers |= 0x0008;
        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        return true;
    }

    private static ToggleShortcutSettings ToSettings(Key key, ModifierKeys modifiers) => new()
    {
        Key = key.ToString(),
        Modifiers = Enum.GetValues<ModifierKeys>()
            .Where(flag => flag is ModifierKeys.Control or ModifierKeys.Alt or ModifierKeys.Shift or ModifierKeys.Windows)
            .Where(flag => (modifiers & flag) != 0)
            .Select(static flag => flag.ToString())
            .ToList()
    };

    private static ModifierKeys ParseModifiers(IEnumerable<string>? modifiers) =>
        (modifiers ?? []).Aggregate(ModifierKeys.None, static (current, item) =>
            current | (Enum.TryParse(item, true, out ModifierKeys parsed) ? parsed : ModifierKeys.None));

    private static bool IsFunctionKey(Key key) => key >= Key.F1 && key <= Key.F24;
}
