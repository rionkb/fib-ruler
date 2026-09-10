namespace FibRuler.Models;

public sealed class ToggleShortcutSettings
{
    public string Key { get; set; } = "F8";

    public List<string> Modifiers { get; set; } = [];
}
