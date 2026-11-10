using System.Text.Json.Serialization;
using DnDMap.Core.Model;

namespace DnDMap.Core.Protocol;

/// <summary>
/// Команда изменения сессии. Единая точка входа для всех клиентов:
/// WPF-мост, браузер мастера и (в будущем) экран стола.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SetMapCommand), "setMap")]
[JsonDerivedType(typeof(AddTokenCommand), "addToken")]
[JsonDerivedType(typeof(MoveTokenCommand), "moveToken")]
[JsonDerivedType(typeof(RemoveTokenCommand), "removeToken")]
[JsonDerivedType(typeof(SetDiameterCommand), "setDiameter")]
[JsonDerivedType(typeof(SetViewCommand), "setView")]
[JsonDerivedType(typeof(ResetViewCommand), "resetView")]
public abstract record Command;

/// <summary>Открыть карту. Ширина и высота — в пикселях файла.</summary>
public sealed record SetMapCommand(
    [property: JsonRequired] string Name,
    [property: JsonRequired] int Width,
    [property: JsonRequired] int Height) : Command;

/// <summary>
/// Добавить токен. Без координат (null) токен появляется в центре карты, а не в (0,0).
/// EntitySize — категория размера, диаметр считается от базового.
/// </summary>
public sealed record AddTokenCommand(
    [property: JsonRequired] string File,
    EntitySize EntitySize = EntitySize.Normal,
    double? X = null,
    double? Y = null) : Command;

/// <summary>Переместить токен: X/Y — центр в пикселях карты.</summary>
public sealed record MoveTokenCommand(
    [property: JsonRequired] string TokenId,
    [property: JsonRequired] double X,
    [property: JsonRequired] double Y) : Command;

public sealed record RemoveTokenCommand([property: JsonRequired] string TokenId) : Command;

/// <summary>Базовый диаметр токена в пикселях эталонного экрана (как старый слайдер).</summary>
public sealed record SetDiameterCommand([property: JsonRequired] double ReferencePx) : Command;

/// <summary>Задать кадр: центр в пикселях карты и зум 1..3.</summary>
public sealed record SetViewCommand(
    [property: JsonRequired] double CenterX,
    [property: JsonRequired] double CenterY,
    [property: JsonRequired] double Zoom) : Command;

public sealed record ResetViewCommand : Command;
