namespace DnDMap.Core.Model;

/// <summary>Настройки сессии и хоста.</summary>
public sealed record SessionOptions
{
    /// <summary>«Эталонный» экран, относительно которого задан базовый диаметр токена.</summary>
    public double ReferenceViewportWidth { get; init; } = 1920;

    public double ReferenceViewportHeight { get; init; } = 1080;

    public double MinZoom { get; init; } = 1.0;

    public double MaxZoom { get; init; } = 3.0;

    /// <summary>
    /// Базовый диаметр токена в пикселях эталонного экрана (поведение как раньше — 100).
    /// Внутри сессии диаметр хранится в пикселях карты.
    /// </summary>
    public double DefaultDiameterReferencePx { get; init; } = 100;

    public double MinDiameterReferencePx { get; init; } = 20;

    public double MaxDiameterReferencePx { get; init; } = 200;

    public int DefaultPort { get; init; } = 5000;
}
