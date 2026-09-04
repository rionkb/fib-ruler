namespace FibRuler.Services;

public static class FibCalculator
{
    public static double CalculateY(double yA, double yB, double ratio)
    {
        double delta = yA - yB;
        return yB + delta * ratio;
    }
}
