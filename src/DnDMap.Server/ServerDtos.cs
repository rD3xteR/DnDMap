namespace DnDMap.Server;

/// <summary>DTO сети веб-пульта: манифест файлов, результат загрузки, ответ на команду.</summary>
public sealed record FileManifestDto(IReadOnlyList<FileEntryDto> Files);

public sealed record FileEntryDto(string Name, long Size, string Hash);

public sealed record UploadResultDto(string Name, long Size, string Hash);

public sealed record CommandResultDto(bool Accepted, long Revision);

/// <summary>CSS-размер окна дисплея (планшета), в котором живёт сессия.</summary>
public sealed record ViewportDto(double Width, double Height);

/// <summary>Запрос на пустую карту-сетку.</summary>
public sealed record BlankMapRequest(int Columns, int Rows);

/// <summary>Созданная пустая карта: имя файла, клетки и пиксельный размер.</summary>
public sealed record BlankMapDto(
    string Name,
    int Columns,
    int Rows,
    int Width,
    int Height,
    long Revision);

public sealed record ErrorDto(string Error, string? Code = null);
