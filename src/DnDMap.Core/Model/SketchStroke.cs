using DnDMap.Core.Geometry;

namespace DnDMap.Core.Model;

/// <summary>
/// Штрих наброска поверх карты: ломаная в пикселях карты.
/// Цвет — "#rrggbb" или <see cref="EraserColor"/> (ластик вырезает уже нарисованное).
/// </summary>
public sealed record SketchStroke(
    string Id,
    string Color,
    double WidthMapPx,
    IReadOnlyList<MapPoint> Points)
{
    /// <summary>Специальный «цвет»: штрих не рисуется, а стирает наброски до карты.</summary>
    public const string EraserColor = "eraser";
}
