namespace DnDMap.Core.Model;

/// <summary>
/// Категория размера существа. Значение — множитель базового диаметра токена
/// (Normal = 1×, Big = 2×, Huge = 3×), поэтому менять значения нельзя без пересчёта.
/// </summary>
public enum EntitySize
{
    Normal = 1,
    Big = 2,
    Huge = 3
}
