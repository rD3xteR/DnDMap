using DnDMap.Core.Geometry;

namespace DnDMap.Core.Tests.Geometry;

/// <summary>
/// Эталон: дословная копия старой математики MapWindow/ViewModel/ZoomPan.cs
/// (до переезда на DnDMap.Core). Нужен, чтобы доказать сохранение поведения.
/// </summary>
public sealed class LegacyView
{
    public double Zoom { get; private set; } = 1.0;

    public double OffsetX { get; private set; }

    public double OffsetY { get; private set; }

    private double _mapWidth;
    private double _mapHeight;
    private double _viewportWidth;
    private double _viewportHeight;

    public void SetMapSize(double width, double height)
    {
        _mapWidth = width;
        _mapHeight = height;
        RecalcOffset();
    }

    public void SetViewportSize(double width, double height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        RecalcOffset();
    }

    public void ResetView()
    {
        Zoom = 1.0;
        OffsetX = 0;
        OffsetY = 0;
        RecalcOffset();
    }

    /// <summary>Старый Pan: дельта в экранных px (dx_canvas × Zoom), карта идёт за курсором.</summary>
    public void Pan(double dxScreen, double dyScreen)
    {
        OffsetX += dxScreen;
        OffsetY += dyScreen;
        RecalcOffset();
    }

    /// <summary>Старый ZoomAtPoint: canvasPoint — в координатах канвы (до RenderTransform).</summary>
    public void ZoomAtPoint(double canvasX, double canvasY, double newZoom)
    {
        var oldZoom = Zoom;
        newZoom = Math.Max(1.0, Math.Min(3.0, newZoom));
        if (Math.Abs(newZoom - oldZoom) < 0.001)
            return;

        var dx = canvasX - (canvasX * (newZoom / oldZoom));
        var dy = canvasY - (canvasY * (newZoom / oldZoom));

        Zoom = newZoom;
        OffsetX += dx;
        OffsetY += dy;
        RecalcOffset();
    }

    /// <summary>Видимый прямоугольник в пикселях карты (пересчёт из Offset).</summary>
    public RectD VisibleRect()
    {
        var (scaledWidth, _) = GetScaledSize();
        if (scaledWidth == 0)
            return default;

        var scale = scaledWidth / _mapWidth;
        return new RectD(-OffsetX / scale, -OffsetY / scale, _viewportWidth / scale, _viewportHeight / scale);
    }

    private (double width, double height) GetScaledSize()
    {
        if (_mapWidth <= 0 || _mapHeight <= 0 || _viewportWidth <= 0 || _viewportHeight <= 0)
            return (0, 0);

        var fit = Math.Min(_viewportWidth / _mapWidth, _viewportHeight / _mapHeight);
        return (_mapWidth * fit * Zoom, _mapHeight * fit * Zoom);
    }

    private void RecalcOffset()
    {
        var (scaledWidth, scaledHeight) = GetScaledSize();
        if (scaledWidth == 0 || scaledHeight == 0)
            return;

        if (scaledWidth > _viewportWidth)
            OffsetX = Math.Max(_viewportWidth - scaledWidth, Math.Min(0, OffsetX));
        else
            OffsetX = (_viewportWidth - scaledWidth) / 2;

        if (scaledHeight > _viewportHeight)
            OffsetY = Math.Max(_viewportHeight - scaledHeight, Math.Min(0, OffsetY));
        else
            OffsetY = (_viewportHeight - scaledHeight) / 2;
    }
}
