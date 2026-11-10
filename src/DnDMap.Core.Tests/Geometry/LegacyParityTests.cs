using DnDMap.Core.Geometry;
using Xunit;

namespace DnDMap.Core.Tests.Geometry;

/// <summary>
/// Доказывает, что переезд на ядро не изменил наблюдаемое поведение:
/// видимый прямоугольник карты совпадает со старой реализацией.
/// </summary>
public class LegacyParityTests
{
    private static void AssertSameRect(RectD expected, RectD actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Width, actual.Width, 6);
        Assert.Equal(expected.Height, actual.Height, 6);
    }

    private static RectD NewVisibleRect(ViewState view, double mapWidth, double mapHeight, double viewportWidth, double viewportHeight)
    {
        var fit = ViewGeometry.FitScale(mapWidth, mapHeight, viewportWidth, viewportHeight);
        return ViewGeometry.VisibleRect(view, fit * view.Zoom, viewportWidth, viewportHeight);
    }

    [Fact]
    public void Fit_MatchesLegacy()
    {
        var legacy = new LegacyView();
        legacy.SetMapSize(1600, 900);
        legacy.SetViewportSize(1920, 1080);

        var view = ViewGeometry.Reset(1600, 900);

        AssertSameRect(legacy.VisibleRect(), NewVisibleRect(view, 1600, 900, 1920, 1080));
    }

    [Fact]
    public void SmallMap_MatchesLegacy()
    {
        var legacy = new LegacyView();
        legacy.SetMapSize(800, 600);
        legacy.SetViewportSize(1920, 1080);

        var view = ViewGeometry.Reset(800, 600);

        AssertSameRect(legacy.VisibleRect(), NewVisibleRect(view, 800, 600, 1920, 1080));
    }

    [Fact]
    public void ZoomFromSlider_MatchesLegacy()
    {
        var legacy = new LegacyView();
        legacy.SetMapSize(1600, 900);
        legacy.SetViewportSize(1920, 1080);
        legacy.ZoomAtPoint(960, 540, 2); // старый слайдер зумил к центру канвы

        var context = new ViewContext(1600, 900, 1920, 1080);
        var view = ViewGeometry.ZoomAtPoint(
            new ViewState(new MapPoint(800, 450), 1),
            context,
            new MapPoint(960, 540),
            2);

        AssertSameRect(legacy.VisibleRect(), NewVisibleRect(view, 1600, 900, 1920, 1080));
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(-100, 0)]
    [InlineData(0, 75)]
    [InlineData(-500, -300)]
    public void Pan_MatchesLegacy(double dx, double dy)
    {
        var legacy = new LegacyView();
        legacy.SetMapSize(1600, 900);
        legacy.SetViewportSize(1920, 1080);
        legacy.ZoomAtPoint(960, 540, 2);
        legacy.Pan(dx, dy);

        var context = new ViewContext(1600, 900, 1920, 1080);
        var view = ViewGeometry.ZoomAtPoint(
            new ViewState(new MapPoint(800, 450), 1),
            context,
            new MapPoint(960, 540),
            2);
        view = ViewGeometry.PanBy(view, context, dx, dy);

        AssertSameRect(legacy.VisibleRect(), NewVisibleRect(view, 1600, 900, 1920, 1080));
    }

    [Fact]
    public void Pan_ClampedAtEdge_MatchesLegacy()
    {
        var legacy = new LegacyView();
        legacy.SetMapSize(1600, 900);
        legacy.SetViewportSize(1920, 1080);
        legacy.ZoomAtPoint(960, 540, 2);
        legacy.Pan(5000, 5000);

        var context = new ViewContext(1600, 900, 1920, 1080);
        var view = ViewGeometry.ZoomAtPoint(
            new ViewState(new MapPoint(800, 450), 1),
            context,
            new MapPoint(960, 540),
            2);
        view = ViewGeometry.PanBy(view, context, 5000, 5000);

        AssertSameRect(legacy.VisibleRect(), NewVisibleRect(view, 1600, 900, 1920, 1080));
    }
}
