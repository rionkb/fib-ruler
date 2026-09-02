namespace FibRuler.Models;

public sealed class FibLevel
{
    private static readonly double[] DefaultRatios =
    [
        0, 0.236, 0.382, 0.500, 0.618, 0.786, 1.000,
        1.272, 1.414, 1.618, 2.000, 2.618, 3.618, 4.236
    ];

    public FibLevel(double ratio, bool enabled, string label)
    {
        Ratio = ratio;
        Enabled = enabled;
        Label = label;
    }

    public double Ratio { get; }

    public bool Enabled { get; set; }

    public string Label { get; }

    public static FibLevel Create(double ratio, bool enabled = true)
    {
        double normalizedRatio = NormalizeRatio(ratio);
        return new FibLevel(
            normalizedRatio,
            enabled,
            normalizedRatio.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
    }

    public static double NormalizeRatio(double ratio) =>
        Math.Round(ratio, 3, MidpointRounding.AwayFromZero);

    public static IReadOnlyList<FibLevel> CreateDefaults() =>
        DefaultRatios.Select(static ratio => Create(ratio)).ToList();

    public static bool IsDefaultRatio(double ratio) =>
        DefaultRatios.Any(defaultRatio => Math.Abs(defaultRatio - ratio) <= 0.0001);

}
