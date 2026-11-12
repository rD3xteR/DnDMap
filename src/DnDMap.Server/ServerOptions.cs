namespace DnDMap.Server;

/// <summary>
/// Параметры хоста. HTTP-сервер: TcpListener + REST + SSE + встроенная SPA.
/// </summary>
public sealed record ServerOptions
{
    /// <summary>Порт. 0 — выбрать свободный автоматически (полезно для тестов).</summary>
    public int Port { get; init; } = 5000;

    /// <summary>0.0.0.0 — доступ из локальной сети, 127.0.0.1 — только с этой машины.</summary>
    public string BindAddress { get; init; } = "0.0.0.0";

    /// <summary>Корень кэша карт и токенов (maps/ и tokens/ внутри).</summary>
    public string StorageRoot { get; init; } = ".";

    /// <summary>Максимальный размер одного загружаемого файла.</summary>
    public long MaxUploadBytes { get; init; } = 256L * 1024 * 1024;
}
