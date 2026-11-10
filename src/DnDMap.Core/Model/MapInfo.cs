namespace DnDMap.Core.Model;

/// <summary>Открытая карта: имя файла в хранилище и её пиксельные размеры.</summary>
public sealed record MapInfo(string Name, int Width, int Height);
