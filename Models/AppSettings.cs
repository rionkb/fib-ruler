namespace FibRuler.Models;

public sealed class AppSettings
{
    public int SettingsVersion { get; set; } = 1;

    public PointSettings PointA { get; set; } = new();

    public PointSettings PointB { get; set; } = new();

    public List<FibLevelSettings> FibLevels { get; set; } = [];
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
}
