namespace DnDMap.Server.Http;

/// <summary>Разобранный HTTP-запрос: путь и параметры уже раскодированы.</summary>
internal sealed class HttpRequest
{
    public required string Method { get; init; }

    /// <summary>Раскодированный путь без query, например "/api/files/map/Карта боя.png".</summary>
    public required string Path { get; init; }

    /// <summary>Сегменты пути (тоже раскодированные), без пустых.</summary>
    public required IReadOnlyList<string> Segments { get; init; }

    public required IReadOnlyDictionary<string, string> Query { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    /// <summary>Тело запроса; для GET — пустой поток.</summary>
    public required Stream Body { get; init; }

    public long ContentLength { get; init; }

    public string? Header(string name) =>
        Headers.TryGetValue(name, out var value) ? value : null;
}
