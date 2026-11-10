namespace DnDMap.Core.Geometry;

/// <summary>
/// Размеры карты и вьюпорта, от которых зависит кадр.
/// Вьюпорт — живой размер экрана/окна/WebView, который регистрирует хост.
/// </summary>
public readonly record struct ViewContext(
    double MapWidth,
    double MapHeight,
    double ViewportWidth,
    double ViewportHeight)
{
    /// <summary>Масштаб «карта целиком вписана во вьюпорт» (экранных px на пиксель карты).</summary>
    public double FitScale => ViewGeometry.FitScale(MapWidth, MapHeight, ViewportWidth, ViewportHeight);

    /// <summary>Размеры заданы и имеют смысл.</summary>
    public bool IsValid => MapWidth > 0 && MapHeight > 0 && ViewportWidth > 0 && ViewportHeight > 0;
}
