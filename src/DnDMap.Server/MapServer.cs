using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using DnDMap.Core.Imaging;
using DnDMap.Core.Model;
using DnDMap.Core.Protocol;
using DnDMap.Core.Storage;
using DnDMap.Server.Http;

namespace DnDMap.Server;

/// <summary>
/// HTTP-хост стола: REST-команды, SSE-состояние, манифест и загрузка файлов,
/// встроенная SPA. Работает на BCL (TcpListener), поэтому годится и для Android.
/// </summary>
public sealed class MapServer : IDisposable
{
    private const long MaxJsonBytes = 1024 * 1024;
    private const int SseQueueLength = 16;
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

    private readonly MapSession _session;
    private readonly PhysicalFileStore _store;
    private readonly FileHashCache _hashes;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<Channel<string>, byte> _clients = new();

    private readonly long _maxUploadBytes;
    private Task? _acceptTask;
    private int _port;

    public MapServer(ServerOptions options, MapSession? session = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _session = session ?? new MapSession();
        _store = new PhysicalFileStore(options.StorageRoot);
        _hashes = new FileHashCache(_store);
        _maxUploadBytes = options.MaxUploadBytes;
        _listener = new TcpListener(IPAddress.Parse(options.BindAddress), options.Port);
        _session.Changed += OnSessionChanged;
    }

    public MapSession Session => _session;

    /// <summary>Фактический порт (актуален после <see cref="Start"/>; при Port=0 выбирается ОС).</summary>
    public int Port => _port;

    public bool IsRunning => _acceptTask is not null;

    /// <summary>Диагностика: ошибки соединений и необработанные исключения.</summary>
    public Action<string>? Log { get; set; }

    public void Start()
    {
        if (_acceptTask is not null)
            throw new InvalidOperationException("Сервер уже запущен.");

        _listener.Start(64);
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_acceptTask is null)
            return;

        _cts.Cancel();
        _listener.Stop();

        try
        {
            await _acceptTask;
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
        {
            // Остановка — ожидаемое исключение.
        }

        _acceptTask = null;

        foreach (var client in _clients.Keys)
            client.Writer.TryComplete();

        _clients.Clear();
    }

    public void Dispose()
    {
        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log?.Invoke($"stop: {ex.Message}");
        }

        _session.Changed -= OnSessionChanged;
        _cts.Dispose();
    }

    // ----- приём соединений -----

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                break;
            }

            _ = HandleConnectionAsync(client, cancellationToken);
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            try
            {
                var request = await HttpProtocol.ReadRequestAsync(stream, cancellationToken);
                if (request is null)
                    return;

                if (request.Method == "GET" && request.Path == "/api/events")
                {
                    await HandleEventsAsync(stream, cancellationToken);
                    return;
                }

                await RouteAsync(request, stream, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException
                                           or ObjectDisposedException or InvalidDataException)
            {
                Log?.Invoke($"connection: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log?.Invoke($"unhandled: {ex}");
            }
        }
    }

    // ----- SSE -----

    private void OnSessionChanged(SessionChange change)
    {
        if (_clients.IsEmpty)
            return;

        string json;

        try
        {
            json = JsonSerializer.Serialize(SessionMapper.ToDto(_session), ProtocolJson.Options);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"sse serialize: {ex.Message}");
            return;
        }

        foreach (var client in _clients.Keys)
            client.Writer.TryWrite(json);
    }

    private async Task HandleEventsAsync(Stream stream, CancellationToken cancellationToken)
    {
        await HttpProtocol.WriteSseHeadersAsync(stream, cancellationToken);

        var queue = Channel.CreateBounded<string>(new BoundedChannelOptions(SseQueueLength)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

        _clients.TryAdd(queue, 0);

        try
        {
            var initial = JsonSerializer.Serialize(SessionMapper.ToDto(_session), ProtocolJson.Options);
            await HttpProtocol.WriteSseEventAsync(stream, "state", initial, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var waitRead = queue.Reader.WaitToReadAsync(cancellationToken).AsTask();
                var ping = Task.Delay(PingInterval, cancellationToken);
                var completed = await Task.WhenAny(waitRead, ping);

                if (completed == ping)
                {
                    await HttpProtocol.WriteSseCommentAsync(stream, cancellationToken);
                    continue;
                }

                if (!await waitRead)
                    break;

                while (queue.Reader.TryRead(out var data))
                    await HttpProtocol.WriteSseEventAsync(stream, "state", data, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException
                                       or SocketException)
        {
            // Клиент ушёл — это штатное завершение потока событий.
        }
        finally
        {
            _clients.TryRemove(queue, out _);
            queue.Writer.TryComplete();
        }
    }

    // ----- маршруты -----

    private async Task RouteAsync(HttpRequest request, Stream stream, CancellationToken cancellationToken)
    {
        var segments = request.Segments;

        if (request.Method == "GET" && EmbeddedAssets.TryOpen(request.Path, out var asset, out var contentType))
        {
            using (asset)
                await HttpProtocol.WriteStreamAsync(stream, 200, contentType, asset.Length, asset, cancellationToken);
            return;
        }

        if (segments is ["api", "session"] && request.Method == "GET")
        {
            await HttpProtocol.WriteJsonAsync(
                stream, 200, SessionMapper.ToDto(_session), cancellationToken);
            return;
        }

        if (segments is ["api", "commands"] && request.Method == "POST")
        {
            await HandleCommandAsync(request, stream, cancellationToken);
            return;
        }

        if (segments is ["api", "files"] && request.Method == "GET")
        {
            await HandleManifestAsync(request, stream, cancellationToken);
            return;
        }

        if (segments.Count == 4 && segments[0] == "api" && segments[1] == "files")
        {
            if (request.Method == "PUT")
            {
                await HandleUploadAsync(segments[2], segments[3], request, stream, cancellationToken);
                return;
            }

            if (request.Method == "DELETE")
            {
                await HandleDeleteAsync(segments[2], segments[3], stream, cancellationToken);
                return;
            }
        }

        if (segments.Count == 3 && segments[0] == "files" && request.Method == "GET")
        {
            await HandleDownloadAsync(segments[1], segments[2], stream, cancellationToken);
            return;
        }

        var known = segments is ["api", "session"] or ["api", "commands"] or ["api", "files"]
                    || (segments.Count >= 3 && segments[0] is "api" or "files");

        await HttpProtocol.WriteErrorAsync(
            stream,
            known ? 405 : 404,
            known ? "Метод не поддерживается." : "Не найдено.",
            cancellationToken);
    }

    private async Task HandleCommandAsync(HttpRequest request, Stream stream, CancellationToken cancellationToken)
    {
        if (request.ContentLength <= 0)
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, "Пустое тело команды.", cancellationToken);
            return;
        }

        if (request.ContentLength > MaxJsonBytes)
        {
            await HttpProtocol.WriteErrorAsync(stream, 413, "Команда слишком большая.", cancellationToken);
            return;
        }

        Command? command;

        try
        {
            command = await JsonSerializer.DeserializeAsync<Command>(
                request.Body, ProtocolJson.Options, cancellationToken);
        }
        catch (JsonException ex)
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, $"Некорректный JSON: {ex.Message}", cancellationToken);
            return;
        }

        if (command is null)
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, "Команда не распознана.", cancellationToken);
            return;
        }

        try
        {
            // Размеры карты берём из файла, а не из запроса клиента.
            if (command is SetMapCommand setMap)
            {
                if (!_store.Exists(FileKind.Map, setMap.Name))
                {
                    await HttpProtocol.WriteErrorAsync(stream, 409, "Карта не загружена на хост.", cancellationToken);
                    return;
                }

                using var image = _store.OpenRead(FileKind.Map, setMap.Name);
                if (!ImageSizeReader.TryRead(image, out var size))
                {
                    await HttpProtocol.WriteErrorAsync(stream, 415, "Файл карты не является PNG/JPEG.", cancellationToken);
                    return;
                }

                command = setMap with { Width = size.Width, Height = size.Height };
            }

            if (command is AddTokenCommand addToken && !_store.Exists(FileKind.Token, addToken.File))
            {
                await HttpProtocol.WriteErrorAsync(stream, 409, "Токен не загружен на хост.", cancellationToken);
                return;
            }
        }
        catch (ArgumentException ex)
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, ex.Message, cancellationToken);
            return;
        }

        var accepted = _session.Apply(command);

        await HttpProtocol.WriteJsonAsync(
            stream,
            accepted ? 200 : 409,
            new CommandResultDto(accepted, _session.Revision),
            cancellationToken);
    }

    private async Task HandleManifestAsync(HttpRequest request, Stream stream, CancellationToken cancellationToken)
    {
        if (!TryParseKind(request.Query.GetValueOrDefault("kind"), out var kind))
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, "kind должен быть map или token.", cancellationToken);
            return;
        }

        var files = _store.List(kind)
            .Select(entry => new FileEntryDto(entry.Name, entry.Size, _hashes.Get(kind, entry.Name)))
            .ToArray();

        await HttpProtocol.WriteJsonAsync(stream, 200, new FileManifestDto(files), cancellationToken);
    }

    private async Task HandleUploadAsync(
        string kindText,
        string name,
        HttpRequest request,
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kindText, out var kind))
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, "Вид файлов: map или token.", cancellationToken);
            return;
        }

        try
        {
            _store.ValidateName(name);
        }
        catch (ArgumentException ex)
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, ex.Message, cancellationToken);
            return;
        }

        if (!IsSupportedImage(name))
        {
            await HttpProtocol.WriteErrorAsync(stream, 415, "Поддерживаются только PNG и JPEG.", cancellationToken);
            return;
        }

        if (request.ContentLength <= 0)
        {
            await HttpProtocol.WriteErrorAsync(stream, 411, "Нужен Content-Length и непустое тело.", cancellationToken);
            return;
        }

        if (request.ContentLength > _maxUploadBytes)
        {
            await HttpProtocol.WriteErrorAsync(stream, 413, "Файл слишком большой.", cancellationToken);
            return;
        }

        _store.Write(kind, name, request.Body);
        _hashes.Invalidate(kind, name);

        // Битые/не-картинки не должны попадать в кэш: по ним нельзя открыть карту.
        using (var check = _store.OpenRead(kind, name))
        {
            if (!ImageSizeReader.TryRead(check, out _))
            {
                check.Dispose();
                _store.Delete(kind, name);
                _hashes.Invalidate(kind, name);
                await HttpProtocol.WriteErrorAsync(stream, 415, "Файл не является PNG/JPEG.", cancellationToken);
                return;
            }
        }

        using var stored = _store.OpenRead(kind, name);
        var result = new UploadResultDto(name, stored.Length, _hashes.Get(kind, name));

        await HttpProtocol.WriteJsonAsync(stream, 200, result, cancellationToken);
    }

    private async Task HandleDeleteAsync(
        string kindText,
        string name,
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kindText, out var kind))
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, "Вид файлов: map или token.", cancellationToken);
            return;
        }

        try
        {
            if (!_store.Delete(kind, name))
            {
                await HttpProtocol.WriteErrorAsync(stream, 404, "Файл не найден.", cancellationToken);
                return;
            }
        }
        catch (ArgumentException ex)
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, ex.Message, cancellationToken);
            return;
        }

        _hashes.Invalidate(kind, name);
        await HttpProtocol.WriteEmptyAsync(stream, 204, cancellationToken);
    }

    private async Task HandleDownloadAsync(
        string kindText,
        string name,
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kindText, out var kind))
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, "Вид файлов: map или token.", cancellationToken);
            return;
        }

        try
        {
            if (!_store.Exists(kind, name))
            {
                await HttpProtocol.WriteErrorAsync(stream, 404, "Файл не найден.", cancellationToken);
                return;
            }

            using var file = _store.OpenRead(kind, name);
            await HttpProtocol.WriteStreamAsync(
                stream, 200, ContentTypeFor(name), file.Length, file, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await HttpProtocol.WriteErrorAsync(stream, 400, ex.Message, cancellationToken);
        }
    }

    // ----- вспомогательное -----

    private static bool TryParseKind(string? text, out FileKind kind)
    {
        kind = text?.ToLowerInvariant() switch
        {
            "map" => FileKind.Map,
            "token" => FileKind.Token,
            _ => (FileKind)(-1)
        };

        return (int)kind >= 0;
    }

    private static bool IsSupportedImage(string name) =>
        PhysicalFileStore.SupportedExtensions.Contains(
            Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    private static string ContentTypeFor(string name) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
}
