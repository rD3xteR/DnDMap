using DnDMap.Core.Geometry;

namespace DnDMap.Core.Model;

/// <summary>
/// Согласованный снимок состояния: все поля прочитаны под одним lock,
/// поэтому Revision и данные всегда соответствуют друг другу.
/// </summary>
public sealed record SessionSnapshot(
    long Revision,
    MapInfo? Map,
    ViewState View,
    double ViewportWidth,
    double ViewportHeight,
    double BaseDiameterMapPx,
    double BaseDiameterReferencePx,
    IReadOnlyList<MapToken> Tokens,
    bool TabletInputLocked,
    IReadOnlyList<SketchStroke> Sketch)
{
    public ViewContext Context => new(Map?.Width ?? 0, Map?.Height ?? 0, ViewportWidth, ViewportHeight);

    public double FitScale => Context.FitScale;

    public RectD VisibleRect => ViewGeometry.VisibleRect(View, FitScale * View.Zoom, ViewportWidth, ViewportHeight);
}
