using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using DnDMap.Core.Protocol;
using Xunit;

namespace DnDMap.Server.Tests;

public sealed class ServerApiTests
{
    private const string SetMapBoard =
        """{"type":"setMap","name":"board.png","width":0,"height":0}""";

    [Fact]
    public async Task Session_InitiallyEmpty()
    {
        await using var server = new ServerFixture();

        var response = await server.Client.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<SessionDto>(ProtocolJson.Options);
        Assert.NotNull(session);
        Assert.Null(session!.Map);
        Assert.Empty(session.Tokens);
        Assert.Equal(0, session.Revision);
        Assert.Equal(1, session.View.Zoom);

        // Без карты кадр не определён: fit = 0, поэтому кадр нулевой.
        Assert.Equal(0, session.VisibleRect.Width);
    }

    [Fact]
    public async Task Static_IndexServedFromAssembly()
    {
        await using var server = new ServerFixture();

        var response = await server.Client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("DnDMap", html);
        Assert.Contains("/app.js", html);

        var script = await server.Client.GetStringAsync("/app.js");
        Assert.Contains("EventSource", script);
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        await using var server = new ServerFixture();

        var response = await server.Client.GetAsync("/nope");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetMap_RequiresUploadedFile()
    {
        await using var server = new ServerFixture();

        var response = await server.SendCommandAsync(SetMapBoard);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SetMap_UsesRealImageSizeFromFile()
    {
        await using var server = new ServerFixture();

        var upload = await server.UploadAsync("map", "board.png", ServerFixture.PngBytes(320, 200));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var command = await server.SendCommandAsync(SetMapBoard);
        Assert.Equal(HttpStatusCode.OK, command.StatusCode);

        var session = await server.Client.GetFromJsonAsync<SessionDto>("/api/session", ProtocolJson.Options);
        Assert.NotNull(session?.Map);
        Assert.Equal("board.png", session!.Map!.Name);
        Assert.Equal(320, session.Map.Width);
        Assert.Equal(200, session.Map.Height);
    }

    [Fact]
    public async Task AddToken_RequiresUploadedTokenAndMap()
    {
        await using var server = new ServerFixture();

        var missingMap = await server.SendCommandAsync("""{"type":"addToken","file":"goblin.png"}""");
        Assert.Equal(HttpStatusCode.Conflict, missingMap.StatusCode);

        await server.UploadAsync("map", "board.png", ServerFixture.PngBytes(320, 200));
        await server.SendCommandAsync(SetMapBoard);

        var missingFile = await server.SendCommandAsync("""{"type":"addToken","file":"goblin.png"}""");
        Assert.Equal(HttpStatusCode.Conflict, missingFile.StatusCode);

        await server.UploadAsync("token", "goblin.png", ServerFixture.PngBytes(64, 64));

        var accepted = await server.SendCommandAsync("""{"type":"addToken","file":"goblin.png","entitySize":"big"}""");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var session = await server.Client.GetFromJsonAsync<SessionDto>("/api/session", ProtocolJson.Options);
        var token = Assert.Single(session!.Tokens);
        Assert.Equal("goblin.png", token.File);

        // «Большой» = 2 диаметра; диаметр — 100 px эталона 1920×1080,
        // на карте 320×200 это 100 / 5.4 ≈ 18.5 px карты, значит ≈37.
        Assert.Equal(37, Math.Round(token.SizeMapPx));
    }

    [Fact]
    public async Task Manifest_ReturnsNameSizeAndSha256()
    {
        await using var server = new ServerFixture();
        var bytes = ServerFixture.PngBytes(40, 30);

        await server.UploadAsync("map", "m.png", bytes);

        var manifest = await server.Client.GetFromJsonAsync<FileManifestDto>(
            "/api/files?kind=map", ProtocolJson.Options);

        var entry = Assert.Single(manifest!.Files);
        Assert.Equal("m.png", entry.Name);
        Assert.Equal(bytes.Length, entry.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), entry.Hash);
    }

    [Fact]
    public async Task Download_ReturnsUploadedBytes()
    {
        await using var server = new ServerFixture();
        var bytes = ServerFixture.PngBytes(12, 34);

        await server.UploadAsync("token", "goblin.png", bytes);

        var response = await server.Client.GetAsync("/files/token/goblin.png");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Delete_RemovesFile()
    {
        await using var server = new ServerFixture();

        await server.UploadAsync("map", "m.png", ServerFixture.PngBytes(10, 10));

        var delete = await server.Client.DeleteAsync("/api/files/map/m.png");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var download = await server.Client.GetAsync("/files/map/m.png");
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);

        var manifest = await server.Client.GetFromJsonAsync<FileManifestDto>(
            "/api/files?kind=map", ProtocolJson.Options);
        Assert.Empty(manifest!.Files);
    }

    [Fact]
    public async Task Upload_RejectsPathTraversalAndBadNames()
    {
        await using var server = new ServerFixture();

        var traversal = await server.UploadAsync("map", "../x.png", ServerFixture.PngBytes(5, 5));
        Assert.Equal(HttpStatusCode.BadRequest, traversal.StatusCode);

        var reserved = await server.UploadAsync("map", "CON.png", ServerFixture.PngBytes(5, 5));
        Assert.Equal(HttpStatusCode.BadRequest, reserved.StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsUnsupportedExtension()
    {
        await using var server = new ServerFixture();

        var response = await server.UploadAsync("map", "note.txt", Encoding.UTF8.GetBytes("hello"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsBrokenImageAndCleansUp()
    {
        await using var server = new ServerFixture();

        var response = await server.UploadAsync("map", "fake.png", Encoding.UTF8.GetBytes("not an image"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);

        var manifest = await server.Client.GetFromJsonAsync<FileManifestDto>(
            "/api/files?kind=map", ProtocolJson.Options);
        Assert.Empty(manifest!.Files);
    }

    [Fact]
    public async Task Upload_AcceptsLaptopNames()
    {
        await using var server = new ServerFixture();
        var bytes = ServerFixture.PngBytes(20, 10);

        var upload = await server.UploadAsync("map", "Карта подземелья (ночь).png", bytes);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var response = await server.Client.GetAsync("/files/map/Карта подземелья (ночь).png");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Sse_SendsInitialStateAndBroadcastsChanges()
    {
        await using var server = new ServerFixture();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/events");
        using var response = await server.Client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);

        var initial = await ReadEventAsync(reader, timeout.Token);
        Assert.Contains("\"revision\":0", initial);

        await server.UploadAsync("map", "board.png", ServerFixture.PngBytes(100, 100));
        var command = await server.SendCommandAsync(SetMapBoard);
        Assert.Equal(HttpStatusCode.OK, command.StatusCode);

        var update = await ReadEventAsync(reader, timeout.Token);
        Assert.Contains("\"revision\":1", update);
        Assert.Contains("board.png", update);
    }

    [Fact]
    public async Task WrongMethod_Returns405()
    {
        await using var server = new ServerFixture();

        var response = await server.Client.DeleteAsync("/api/session");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Viewport_ChangesFitAndVisibleRect()
    {
        await using var server = new ServerFixture();

        await server.UploadAsync("map", "board.png", ServerFixture.PngBytes(320, 200));
        await server.SendCommandAsync(SetMapBoard);

        var response = await server.Client.PostAsync(
            "/api/viewport",
            new StringContent("""{"width":1000,"height":1000}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await server.Client.GetFromJsonAsync<SessionDto>("/api/session", ProtocolJson.Options);

        // fit = min(1000/320, 1000/200) = 3.125 → видимая область 320×320.
        Assert.Equal(320, session!.VisibleRect.Width, 6);
        Assert.Equal(320, session.VisibleRect.Height, 6);
    }

    [Fact]
    public async Task Viewport_RejectsInvalidSize()
    {
        await using var server = new ServerFixture();

        var response = await server.Client.PostAsync(
            "/api/viewport",
            new StringContent("""{"width":0,"height":1000}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<string> ReadEventAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var data = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new EndOfStreamException("SSE-поток закрылся раньше события.");

            if (line.Length == 0)
                return data.ToString();

            if (line.StartsWith("data: ", StringComparison.Ordinal))
                data.Append(line[6..]);
        }
    }
}
