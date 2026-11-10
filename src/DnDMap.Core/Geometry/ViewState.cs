namespace DnDMap.Core.Geometry;

/// <summary>
/// Кадр: центр видимой области в пикселях карты и зум относительно масштаба
/// «карта целиком» (1× — карта вписана в экран, 3× — максимальное приближение).
/// </summary>
public readonly record struct ViewState(MapPoint Center, double Zoom)
{
    /// <summary>Кадр по умолчанию для карты заданного размера: центр, 1×.</summary>
    public static ViewState ForMap(double mapWidth, double mapHeight) =>
        new(new MapPoint(mapWidth / 2.0, mapHeight / 2.0), ViewGeometry.MinZoom);
}
