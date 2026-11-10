namespace DnDMap.Server;

/// <summary>
/// Параметры хоста. Сам HTTP-сервер (TcpListener + REST + SSE + статика SPA)
/// реализуется в M2; здесь зафиксированы только границы и значения по умолчанию.
/// </summary>
public sealed record ServerOptions
{
    /// <summary>Порт по умолчанию. 0 — выбрать свободный автоматически.</summary>
    public int Port { get; init; } = 5000;

    /// <summary>0.0.0.0 — доступ из локальной сети, 127.0.0.1 — только с этой машины.</summary>
    public string BindAddress { get; init; } = "0.0.0.0";

    /// <summary>Корень библиотеки карт и токенов (maps/ и tokens/ внутри).</summary>
    public string StorageRoot { get; init; } = ".";
}
