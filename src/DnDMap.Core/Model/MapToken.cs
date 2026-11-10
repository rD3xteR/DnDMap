namespace DnDMap.Core.Model;

/// <summary>
/// Токен на карте. X/Y — центр токена в пикселях карты, Size — диаметр в пикселях карты.
/// Координаты не зависят от экрана: любой клиент пересчитывает их в свои экранные.
/// </summary>
public sealed class MapToken
{
    public MapToken(string id, string file, EntitySize entitySize, double x, double y)
    {
        Id = id;
        File = file;
        EntitySize = entitySize;
        X = x;
        Y = y;
    }

    public string Id { get; }

    public string File { get; }

    public double X { get; internal set; }

    public double Y { get; internal set; }

    public double Size { get; internal set; }

    public EntitySize EntitySize { get; internal set; }
}
