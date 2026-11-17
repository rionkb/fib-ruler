namespace FibRuler.Models;

public sealed class AppSettings
{
    public int SettingsVersion { get; set; } = 1;

    public PointSettings PointA { get; set; } = new();

    public PointSettings PointB { get; set; } = new();

    public bool HasPlacedRuler { get; set; }

    public List<FibLevelSettings> FibLevels { get; set; } = [];

    public ToggleShortcutSettings? DrawShortcut { get; set; }

    public ToggleShortcutSettings? DeleteShortcut { get; set; }

    public ColorSettings? Colors { get; set; }

    public double? BandOpacity { get; set; }
}

public sealed class PointSettings
{
    public double X { get; set; }

    public double Y { get; set; }
}

public sealed class FibLevelSettings
{
    public double Ratio { get; set; }

    public bool Enabled { get; set; }

    public bool IsCustom { get; set; }
}

public sealed class ColorSettings
{
    public const double DefaultBandOpacity = 0.2;

    public static readonly string[] ColorKeys =
    [
        "Base", "Red", "Green", "Orange", "Cyan", "Blue", "Purple", "Pink"
    ];

    public string BaseLevelColor { get; set; } = "#808080";

    public string RedColor { get; set; } = "#F23645";

    public string GreenColor { get; set; } = "#4CAF50";

    public string OrangeColor { get; set; } = "#FF9800";

    public string CyanColor { get; set; } = "#00BCD4";

    public string BlueColor { get; set; } = "#2962FF";

    public string PurpleColor { get; set; } = "#9C27B0";

    public string PinkColor { get; set; } = "#E91E63";

    public static ColorSettings CreateDefault() => new();

    public string GetColor(string key) => key switch
    {
        "Red" => RedColor,
        "Green" => GreenColor,
        "Orange" => OrangeColor,
        "Cyan" => CyanColor,
        "Blue" => BlueColor,
        "Purple" => PurpleColor,
        "Pink" => PinkColor,
        _ => BaseLevelColor
    };

    public void SetColor(string key, string value)
    {
        switch (key)
        {
            case "Red": RedColor = value; break;
            case "Green": GreenColor = value; break;
            case "Orange": OrangeColor = value; break;
            case "Cyan": CyanColor = value; break;
            case "Blue": BlueColor = value; break;
            case "Purple": PurpleColor = value; break;
            case "Pink": PinkColor = value; break;
            default: BaseLevelColor = value; break;
        }
    }

    public static ColorSettings Clone(ColorSettings colors)
    {
        ColorSettings clone = new();
        foreach (string key in ColorKeys)
        {
            clone.SetColor(key, colors.GetColor(key));
        }

        return clone;
    }

    public static bool TryNormalizeHex(string? value, out string normalized)
    {
        normalized = string.Empty;
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.StartsWith('#'))
        {
            candidate = candidate[1..];
        }

        if (candidate.Length != 6 || candidate.Any(static character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        normalized = $"#{candidate.ToUpperInvariant()}";
        return true;
    }
}
