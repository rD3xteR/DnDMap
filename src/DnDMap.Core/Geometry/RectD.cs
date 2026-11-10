namespace DnDMap.Core.Geometry;

/// <summary>Прямоугольник (X/Y — левый верхний угол).</summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Left => X;

    public double Top => Y;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public MapPoint Center => new(X + (Width / 2.0), Y + (Height / 2.0));

    public bool Contains(MapPoint point, double tolerance = 1e-9) =>
        point.X >= Left - tolerance && point.X <= Right + tolerance &&
        point.Y >= Top - tolerance && point.Y <= Bottom + tolerance;
}
