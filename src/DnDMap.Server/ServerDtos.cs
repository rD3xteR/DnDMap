namespace DnDMap.Server;

/// <summary>DTO сети веб-пульта: манифест файлов, результат загрузки, ответ на команду.</summary>
public sealed record FileManifestDto(IReadOnlyList<FileEntryDto> Files);

public sealed record FileEntryDto(string Name, long Size, string Hash);

public sealed record UploadResultDto(string Name, long Size, string Hash);

public sealed record CommandResultDto(bool Accepted, long Revision);

public sealed record ErrorDto(string Error, string? Code = null);
