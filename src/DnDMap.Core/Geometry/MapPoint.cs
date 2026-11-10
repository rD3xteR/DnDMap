namespace DnDMap.Core.Geometry;

/// <summary>
/// Точка в пикселях карты. Не зависит от экрана и вьюпорта — это общая система
/// координат для WPF, веб-клиента и Android-хоста.
/// </summary>
public readonly record struct MapPoint(double X, double Y)
{
    public static MapPoint Zero => default;

    public static MapPoint operator +(MapPoint a, MapPoint b) => new(a.X + b.X, a.Y + b.Y);

    public static MapPoint operator -(MapPoint a, MapPoint b) => new(a.X - b.X, a.Y - b.Y);

    public static MapPoint operator *(MapPoint point, double factor) => new(point.X * factor, point.Y * factor);

    public double Length => Math.Sqrt((X * X) + (Y * Y));
}
