namespace DnDMap.Core.Extensions;

public static class Extensions
{
    public static bool Between(this double x, double min, double max) =>
        max > x && x > min;

    public static bool Equals(this double x, double y) =>
        Math.Abs(x - y) <= Tolerance;

    private const double Tolerance = 1e-6;
}