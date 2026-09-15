namespace FibRuler.Models;

public sealed class ToggleShortcutSettings
{
    public string Key { get; set; } = string.Empty;

    public List<string> Modifiers { get; set; } = [];
}
