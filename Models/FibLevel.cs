namespace FibRuler.Models;

public sealed record FibLevel(double Ratio, bool Enabled, string Label)
{
    public static IReadOnlyList<FibLevel> CreateDefaults() =>
    [
        Create(-1.618), Create(-1.000), Create(-0.618), Create(-0.382), Create(-0.236),
        Create(0), Create(0.236), Create(0.382), Create(0.500), Create(0.618), Create(0.786),
        Create(1.000), Create(1.272), Create(1.414), Create(1.618), Create(2.000),
        Create(2.618), Create(3.618), Create(4.236)
    ];

    private static FibLevel Create(double ratio) =>
        new(ratio, true, ratio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
}
