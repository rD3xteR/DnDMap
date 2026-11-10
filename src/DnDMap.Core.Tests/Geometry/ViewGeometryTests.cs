using DnDMap.Core.Geometry;
using Xunit;

namespace DnDMap.Core.Tests.Geometry;

public class ViewGeometryTests
{
    [Fact]
    public void FitScale_UsesMinAxis()
    {
        Assert.Equal(1.2, ViewGeometry.FitScale(1600, 900, 1920, 1080), 6);
        Assert.Equal(1.8, ViewGeometry.FitScale(800, 600, 1920, 1080), 6);
        Assert.Equal(0.36, ViewGeometry.FitScale(4000, 3000, 1920, 1080), 6);
    }

    [Fact]
    public void FitScale_InvalidSizes_ReturnsZero()
    {
        Assert.Equal(0, ViewGeometry.FitScale(0, 900, 1920, 1080));
        Assert.Equal(0, ViewGeometry.FitScale(1600, -1, 1920, 1080));
        Assert.Equal(0, ViewGeometry.FitScale(1600, 900, 0, 1080));
    }

    [Theory]
    [InlineData(1600, 900, 1920, 1080, 800, 450, 1.0)]
    [InlineData(800, 600, 1920, 1080, 400, 300, 1.0)]
    [InlineData(4000, 3000, 1920, 1080, 2000, 1500, 2.0)]
    public void MapToScreen_ScreenToMap_RoundTrip(
        double mapWidth,
        double mapHeight,
        double viewportWidth,
        double viewportHeight,
        double centerX,
        double centerY,
        double zoom)
    {
        var context = new ViewContext(mapWidth, mapHeight, viewportWidth, viewportHeight);
        var center = new MapPoint(centerX, centerY);
        var scale = context.FitScale * zoom;

        MapPoint[] points =
        [
            new(0, 0),
            new(mapWidth, mapHeight),
            new(mapWidth / 3.0, mapHeight / 7.0)
        ];

        foreach (var point in points)
        {
            var screen = ViewGeometry.MapToScreen(point, center, scale, viewportWidth, viewportHeight);
            var back = ViewGeometry.ScreenToMap(screen, center, scale, viewportWidth, viewportHeight);

            Assert.Equal(point.X, back.X, 6);
            Assert.Equal(point.Y, back.Y, 6);
        }
    }

    [Fact]
    public void VisibleRect_AtZoomOne_ForExactAspectRatio()
    {
        var view = new ViewState(new MapPoint(800, 450), 1);

        var rect = ViewGeometry.VisibleRect(view, 1.2, 1920, 1080);

        Assert.Equal(0, rect.X, 6);
        Assert.Equal(0, rect.Y, 6);
        Assert.Equal(1600, rect.Width, 6);
        Assert.Equal(900, rect.Height, 6);
    }

    [Fact]
    public void VisibleRect_AtZoomTwo_ShowsCentralQuarter()
    {
        var view = new ViewState(new MapPoint(800, 450), 2);

        var rect = ViewGeometry.VisibleRect(view, 2.4, 1920, 1080);

        Assert.Equal(400, rect.X, 6);
        Assert.Equal(225, rect.Y, 6);
        Assert.Equal(800, rect.Width, 6);
        Assert.Equal(450, rect.Height, 6);
    }

    [Fact]
    public void ClampAxis_Centers_WhenMapSmallerThanViewport()
    {
        Assert.Equal(400, ViewGeometry.ClampAxis(9999, 800, 1920, 1.8), 6);
        Assert.Equal(400, ViewGeometry.ClampAxis(-9999, 800, 1920, 1.8), 6);
        Assert.Equal(300, ViewGeometry.ClampAxis(9999, 600, 1080, 1.8), 6);
    }

    [Fact]
    public void ClampAxis_LimitsCenter_WhenMapLargerThanViewport()
    {
        // s = 2.4 → half = 400, допустимый диапазон центра [400, 1200].
        Assert.Equal(1200, ViewGeometry.ClampAxis(5000, 1600, 1920, 2.4), 6);
        Assert.Equal(400, ViewGeometry.ClampAxis(-100, 1600, 1920, 2.4), 6);
        Assert.Equal(800, ViewGeometry.ClampAxis(800, 1600, 1920, 2.4), 6);
    }

    [Fact]
    public void ClampAxis_RangeIsNotEmpty_WhenMapBarelyCoversViewport()
    {
        // mapLen * s == viewLen → карта ровно по экрану, центр жёстко посередине.
        Assert.Equal(800, ViewGeometry.ClampAxis(0, 1600, 1920, 1.2), 6);
        Assert.Equal(800, ViewGeometry.ClampAxis(1600, 1600, 1920, 1.2), 6);
    }

    [Fact]
    public void Pan_MovesMapByExactScreenDelta()
    {
        var context = new ViewContext(1600, 900, 1920, 1080);
        var view = new ViewState(new MapPoint(800, 450), 2);

        var moved = ViewGeometry.PanBy(view, context, 100, 0);

        // 100 экранных px при s = 2.4 → 41.6667 px карты.
        Assert.Equal(800 - (100 / 2.4), moved.Center.X, 6);
        Assert.Equal(450, moved.Center.Y, 6);
        Assert.Equal(2, moved.Zoom);

        var before = ViewGeometry.VisibleRect(view, 2.4, 1920, 1080);
        var after = ViewGeometry.VisibleRect(moved, 2.4, 1920, 1080);

        Assert.Equal(100, (before.X - after.X) * 2.4, 6);
    }

    [Fact]
    public void Pan_ClampedAtMapEdge()
    {
        var context = new ViewContext(1600, 900, 1920, 1080);
        var view = new ViewState(new MapPoint(800, 450), 2);

        var moved = ViewGeometry.PanBy(view, context, 100_000, 100_000);

        Assert.Equal(400, moved.Center.X, 6);
        Assert.Equal(225, moved.Center.Y, 6);
    }

    [Fact]
    public void ZoomAtPoint_KeepsPointUnderCursor()
    {
        var context = new ViewContext(1600, 900, 1920, 1080);
        var view = new ViewState(new MapPoint(800, 450), 1);
        var cursor = new MapPoint(1000, 500);

        var before = ViewGeometry.ScreenToMap(cursor, view.Center, context.FitScale * view.Zoom, 1920, 1080);
        var zoomed = ViewGeometry.ZoomAtPoint(view, context, cursor, 2);
        var after = ViewGeometry.ScreenToMap(cursor, zoomed.Center, context.FitScale * zoomed.Zoom, 1920, 1080);

        Assert.Equal(2, zoomed.Zoom, 6);
        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
        Assert.Equal(816.6666667, zoomed.Center.X, 6);
        Assert.Equal(433.3333333, zoomed.Center.Y, 6);
    }

    [Fact]
    public void ZoomAtPoint_ClampedToMaxZoom()
    {
        var context = new ViewContext(1600, 900, 1920, 1080);
        var view = new ViewState(new MapPoint(800, 450), 2);

        var zoomed = ViewGeometry.ZoomAtPoint(view, context, new MapPoint(960, 540), 99);

        Assert.Equal(3, zoomed.Zoom, 6);
    }

    [Fact]
    public void ZoomAtCenter_DoesNotMoveCenter()
    {
        var context = new ViewContext(1600, 900, 1920, 1080);
        var view = new ViewState(new MapPoint(800, 450), 1);

        var zoomed = ViewGeometry.ZoomAtPoint(view, context, new MapPoint(960, 540), 2);

        Assert.Equal(800, zoomed.Center.X, 6);
        Assert.Equal(450, zoomed.Center.Y, 6);
    }

    [Fact]
    public void Reset_ReturnsToFitAndCenter()
    {
        var view = ViewGeometry.Reset(1600, 900);

        Assert.Equal(1, view.Zoom);
        Assert.Equal(800, view.Center.X);
        Assert.Equal(450, view.Center.Y);
    }

    [Fact]
    public void MapRectToScreen_ScalesRect()
    {
        var rect = ViewGeometry.MapRectToScreen(
            new RectD(0, 0, 1600, 900),
            new MapPoint(800, 450),
            1.2,
            1920,
            1080);

        Assert.Equal(0, rect.X, 6);
        Assert.Equal(0, rect.Y, 6);
        Assert.Equal(1920, rect.Width, 6);
        Assert.Equal(1080, rect.Height, 6);
    }
}
