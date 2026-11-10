using DnDMap.Core.Model;

namespace DnDMap.Core.Protocol;

/// <summary>Полное состояние сессии — снимок, который получает каждый новый клиент.</summary>
public sealed record SessionDto(
    long Revision,
    MapDto? Map,
    ViewDto View,
    RectDto VisibleRect,
    DiameterDto BaseDiameter,
    IReadOnlyList<TokenDto> Tokens);

public sealed record MapDto(string Name, int Width, int Height);

public sealed record TokenDto(
    string Id,
    string File,
    double X,
    double Y,
    double SizeMapPx,
    EntitySize EntitySize);

public sealed record ViewDto(double CenterX, double CenterY, double Zoom);

/// <summary>Прямоугольник в пикселях карты.</summary>
public sealed record RectDto(double X, double Y, double Width, double Height);

public sealed record DiameterDto(double MapPx, double ReferencePx);
