using DnDMap.Core.Model;

namespace DnDMap.Core.Protocol;

/// <summary>Преобразование состояния сессии в DTO для сети.</summary>
public static class SessionMapper
{
    public static SessionDto ToDto(SessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var visibleRect = snapshot.VisibleRect;

        return new SessionDto(
            snapshot.Revision,
            snapshot.Map is null
                ? null
                : new MapDto(snapshot.Map.Name, snapshot.Map.Width, snapshot.Map.Height),
            new ViewDto(snapshot.View.Center.X, snapshot.View.Center.Y, snapshot.View.Zoom),
            new RectDto(visibleRect.X, visibleRect.Y, visibleRect.Width, visibleRect.Height),
            new DiameterDto(snapshot.BaseDiameterMapPx, snapshot.BaseDiameterReferencePx),
            snapshot.Tokens
                .Select(token => new TokenDto(
                    token.Id,
                    token.File,
                    token.X,
                    token.Y,
                    token.Size,
                    token.EntitySize))
                .ToArray(),
            snapshot.TabletInputLocked);
    }

    public static SketchDto ToSketchDto(SessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new SketchDto(snapshot.Sketch
            .Select(stroke => new SketchStrokeDto(
                stroke.Id,
                stroke.Color,
                stroke.WidthMapPx,
                stroke.Points.Select(point => new PointDto(point.X, point.Y)).ToArray()))
            .ToArray());
    }

    public static SketchDto ToSketchDto(MapSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ToSketchDto(session.Snapshot());
    }

    public static SessionDto ToDto(MapSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ToDto(session.Snapshot());
    }
}
