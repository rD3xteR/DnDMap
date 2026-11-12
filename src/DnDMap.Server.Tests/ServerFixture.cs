using System.Buffers.Binary;
using System.Text;
using DnDMap.Server;

namespace DnDMap.Server.Tests;

/// <summary>Поднимает настоящий сервер на свободном порту и временной папке.</summary>
internal sealed class ServerFixture : IAsyncDisposable
{
    private readonly string _root;
    private readonly MapServer _server;

    public ServerFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), "dndmap-server-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _server = new MapServer(new ServerOptions
        {
            Port = 0,
            BindAddress = "127.0.0.1",
            StorageRoot = _root
        });

        _server.Start();

        Client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_server.Port}") };
        Client.Timeout = TimeSpan.FromSeconds(10);
    }

    public MapServer Server => _server;

    public HttpClient Client { get; }

    /// <summary>Минимальный PNG-заголовок: сигнатура + IHDR с размерами (для ImageSizeReader).</summary>
    public static byte[] PngBytes(int width, int height)
    {
        var bytes = new byte[24];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);

        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }

    public Task<HttpResponseMessage> UploadAsync(string kind, string name, byte[] bytes) =>
        Client.PutAsync(
            $"/api/files/{kind}/{Uri.EscapeDataString(name)}",
            new ByteArrayContent(bytes));

    public Task<HttpResponseMessage> SendCommandAsync(string json) =>
        Client.PostAsync(
            "/api/commands",
            new StringContent(json, Encoding.UTF8, "application/json"));

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _server.StopAsync();
        _server.Dispose();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Временную папку уберёт ОС.
        }
    }
}
