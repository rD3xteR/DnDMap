using DnDMap.Core.Model;

namespace DnDMap.Core.Protocol;

/// <summary>Полное состояние сессии — снимок, который получает каждый новый клиент.</summary>
public sealed record SessionDto(
    long Revision,
    MapDto? Map,
    ViewDto View,
    RectDto VisibleRect,
    DiameterDto BaseDiameter,
    IReadOnlyList<TokenDto> Tokens,
    bool TabletInputLocked);

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

/// <summary>Наброски поверх карты. Ходят отдельным SSE-событием: они редко меняются, но бывают большими.</summary>
public sealed record SketchDto(IReadOnlyList<SketchStrokeDto> Strokes);

public sealed record SketchStrokeDto(
    string Id,
    string Color,
    double WidthMapPx,
    IReadOnlyList<PointDto> Points);

public sealed record PointDto(double X, double Y);
