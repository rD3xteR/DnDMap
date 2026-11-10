namespace DnDMap.Core.Geometry;

/// <summary>
/// Вся математика кадра: вписывание карты, ограничение панорамы, зум к точке,
/// пересчёт экран ↔ карта. Единственный источник правды по координатам —
/// WPF-мост и веб-клиент не дублируют эту логику.
/// </summary>
public static class ViewGeometry
{
    public const double MinZoom = 1.0;
    public const double MaxZoom = 3.0;
    public const double Epsilon = 1e-9;

    public static double ClampZoom(double zoom) => ClampZoom(zoom, MinZoom, MaxZoom);

    public static double ClampZoom(double zoom, double minZoom, double maxZoom) =>
        Math.Max(minZoom, Math.Min(maxZoom, zoom));

    /// <summary>Масштаб «карта целиком вписана во вьюпорт». 0 — если размеры некорректны.</summary>
    public static double FitScale(double mapWidth, double mapHeight, double viewportWidth, double viewportHeight)
    {
        if (mapWidth <= 0 || mapHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
            return 0;

        return Math.Min(viewportWidth / mapWidth, viewportHeight / mapHeight);
    }

    /// <summary>
    /// Ограничение центра по одной оси: карта не уезжает за край экрана,
    /// а если она меньше экрана — центрируется.
    /// </summary>
    public static double ClampAxis(double center, double mapLength, double viewportLength, double scale)
    {
        if (mapLength <= 0 || viewportLength <= 0 || scale <= 0)
            return center;

        // Карта меньше экрана по этой оси — центрируем.
        if (mapLength * scale <= viewportLength)
            return mapLength / 2.0;

        // Видимая область — [center - half, center + half] в пикселях карты;
        // она не должна выходить за [0, mapLength].
        var half = viewportLength / (2.0 * scale);
        return Math.Min(Math.Max(center, half), mapLength - half);
    }

    public static MapPoint ClampCenter(MapPoint center, ViewContext context, double scale) =>
        new(ClampAxis(center.X, context.MapWidth, context.ViewportWidth, scale),
            ClampAxis(center.Y, context.MapHeight, context.ViewportHeight, scale));

    public static MapPoint MapToScreen(
        MapPoint mapPoint,
        MapPoint center,
        double scale,
        double viewportWidth,
        double viewportHeight) =>
        new(((mapPoint.X - center.X) * scale) + (viewportWidth / 2.0),
            ((mapPoint.Y - center.Y) * scale) + (viewportHeight / 2.0));

    public static MapPoint ScreenToMap(
        MapPoint screenPoint,
        MapPoint center,
        double scale,
        double viewportWidth,
        double viewportHeight) =>
        new(((screenPoint.X - (viewportWidth / 2.0)) / scale) + center.X,
            ((screenPoint.Y - (viewportHeight / 2.0)) / scale) + center.Y);

    /// <summary>Прямоугольник карты на экране (для отрисовки изображения карты).</summary>
    public static RectD MapRectToScreen(
        RectD mapRect,
        MapPoint center,
        double scale,
        double viewportWidth,
        double viewportHeight)
    {
        var topLeft = MapToScreen(new MapPoint(mapRect.X, mapRect.Y), center, scale, viewportWidth, viewportHeight);
        return new RectD(topLeft.X, topLeft.Y, mapRect.Width * scale, mapRect.Height * scale);
    }

    /// <summary>
    /// Зум к точке: точка карты под курсором (или пальцем) остаётся под ним.
    /// </summary>
    public static ViewState ZoomAtPoint(ViewState view, ViewContext context, MapPoint screenPoint, double newZoom)
    {
        var zoom = ClampZoom(newZoom);
        if (!context.IsValid)
            return view with { Zoom = zoom };

        var oldScale = context.FitScale * view.Zoom;
        var newScale = context.FitScale * zoom;
        if (Math.Abs(newScale - oldScale) < Epsilon)
            return view with { Zoom = zoom };

        var mapPoint = ScreenToMap(screenPoint, view.Center, oldScale, context.ViewportWidth, context.ViewportHeight);

        var center = new MapPoint(
            mapPoint.X - ((screenPoint.X - (context.ViewportWidth / 2.0)) / newScale),
            mapPoint.Y - ((screenPoint.Y - (context.ViewportHeight / 2.0)) / newScale));

        return new ViewState(ClampCenter(center, context, newScale), zoom);
    }

    /// <summary>
    /// Панорама: карта сдвигается ровно на указанное число экранных пикселей
    /// (положительное dx — вправо). После сдвига кадр ограничивается.
    /// </summary>
    public static ViewState PanBy(ViewState view, ViewContext context, double dxScreen, double dyScreen)
    {
        var scale = context.FitScale * view.Zoom;
        if (!context.IsValid || scale <= 0)
            return view;

        var center = new MapPoint(view.Center.X - (dxScreen / scale), view.Center.Y - (dyScreen / scale));
        return view with { Center = ClampCenter(center, context, scale) };
    }

    /// <summary>Сброс кадра: 1×, карта по центру.</summary>
    public static ViewState Reset(double mapWidth, double mapHeight) => ViewState.ForMap(mapWidth, mapHeight);

    /// <summary>
    /// Видимая область карты в пикселях карты — это то, что хост публикует клиентам,
    /// чтобы они рисовали одинаковый кадр.
    /// </summary>
    public static RectD VisibleRect(ViewState view, double scale, double viewportWidth, double viewportHeight)
    {
        if (scale <= 0)
            return default;

        var width = viewportWidth / scale;
        var height = viewportHeight / scale;
        return new RectD(view.Center.X - (width / 2.0), view.Center.Y - (height / 2.0), width, height);
    }
}
