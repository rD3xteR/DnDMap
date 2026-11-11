using System.Windows;
using DnDMap.Core.Geometry;
using DnDMap.Core.Protocol;
using Point = System.Windows.Point;

namespace DnDMap.MapWindow.ViewModel;

/// <summary>
/// Кадр: зум, панорама и сброс. Вся математика — в <see cref="ViewGeometry"/>,
/// здесь только перевод экранных координат в команды сессии.
/// </summary>
public partial class MapViewModel
{
    public void SetViewportSize(double width, double height)
    {
        _session.SetViewport(width, height);
    }

    public void ResetView()
    {
        _session.Apply(new ResetViewCommand());
    }

    /// <summary>Зум к экранной точке: точка под курсором/пальцем остаётся на месте.</summary>
    public void ZoomAt(Point screenPoint, double zoom)
    {
        var snapshot = _session.Snapshot();
        if (snapshot.Map is null)
            return;

        var view = ViewGeometry.ZoomAtPoint(
            snapshot.View,
            snapshot.Context,
            new MapPoint(screenPoint.X, screenPoint.Y),
            zoom);

        ApplyView(view);
    }

    /// <summary>Зум к центру вьюпорта — для слайдера.</summary>
    public void ZoomTo(double zoom) =>
        ZoomAt(new Point(_session.ViewportWidth / 2.0, _session.ViewportHeight / 2.0), zoom);

    /// <summary>Сдвиг карты ровно на столько экранных пикселей.</summary>
    public void PanByScreen(double dx, double dy)
    {
        var snapshot = _session.Snapshot();
        if (snapshot.Map is null)
            return;

        ApplyView(ViewGeometry.PanBy(snapshot.View, snapshot.Context, dx, dy));
    }

    public void SetDiameter(double referencePx)
    {
        ReferenceDiameter = Math.Clamp(
            referencePx,
            _session.Options.MinDiameterReferencePx,
            _session.Options.MaxDiameterReferencePx);

        if (_session.Map is not null)
            _session.Apply(new SetDiameterCommand(ReferenceDiameter));
    }

    private void ApplyView(ViewState view)
    {
        var current = _session.View;
        if (Math.Abs(view.Zoom - current.Zoom) < ViewGeometry.Epsilon &&
            Math.Abs(view.Center.X - current.Center.X) < ViewGeometry.Epsilon &&
            Math.Abs(view.Center.Y - current.Center.Y) < ViewGeometry.Epsilon)
            return;

        _session.Apply(new SetViewCommand(view.Center.X, view.Center.Y, view.Zoom));
    }
}
