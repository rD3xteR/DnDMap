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
                .ToArray());
    }

    public static SessionDto ToDto(MapSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ToDto(session.Snapshot());
    }
}
