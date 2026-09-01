namespace FibRuler.Models;

public sealed class FibLevel
{
    public FibLevel(double ratio, bool enabled, string label)
    {
        Ratio = ratio;
        Enabled = enabled;
        Label = label;
    }

    public double Ratio { get; }

    public bool Enabled { get; set; }

    public string Label { get; }

    public static IReadOnlyList<FibLevel> CreateDefaults() =>
    [
        Create(-1.618), Create(-1.000), Create(-0.618), Create(-0.382), Create(-0.236),
        Create(0), Create(0.236), Create(0.382), Create(0.500), Create(0.618), Create(0.786),
        Create(1.000), Create(1.272), Create(1.414), Create(1.618), Create(2.000),
        Create(2.618), Create(3.618), Create(4.236)
    ];

    private static FibLevel Create(double ratio) =>
        new(ratio, true, ratio.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
}
