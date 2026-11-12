using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DnDMap.Core.Protocol;

namespace DnDMap.Server.Http;

/// <summary>
/// Минимальный HTTP/1.1: разбор заголовков, потоковое тело, ответы с Content-Length.
/// Соединение закрывается после ответа (кроме SSE), этого достаточно для локального хоста.
/// </summary>
internal static class HttpProtocol
{
    private const int MaxHeaderBytes = 32 * 1024;

    public static async Task<HttpRequest?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new List<byte>(1024);
        var chunk = new byte[4096];
        var headerEnd = -1;

        while (headerEnd < 0)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
                return null;

            buffer.AddRange(chunk.AsSpan(0, read).ToArray());
            headerEnd = FindHeaderEnd(buffer);

            if (headerEnd < 0 && buffer.Count > MaxHeaderBytes)
                throw new InvalidDataException("Слишком большой HTTP-заголовок.");
        }

        var headerText = Encoding.ASCII.GetString(buffer.ToArray(), 0, headerEnd);
        var prefix = buffer.Skip(headerEnd + 4).ToArray();
        var lines = headerText.Split("\r\n");

        var requestLine = lines[0].Split(' ');
        if (requestLine.Length < 3 || !requestLine[2].StartsWith("HTTP/", StringComparison.Ordinal))
            throw new InvalidDataException("Некорректная строка запроса.");

        var method = requestLine[0].ToUpperInvariant();
        var target = requestLine[1];
        var queryIndex = target.IndexOf('?');
        var rawPath = queryIndex >= 0 ? target[..queryIndex] : target;
        var rawQuery = queryIndex >= 0 ? target[(queryIndex + 1)..] : string.Empty;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            if (line.Length == 0)
                continue;

            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        var contentLength = 0L;
        if (headers.TryGetValue("Content-Length", out var lengthText) &&
            !long.TryParse(lengthText, out contentLength))
            throw new InvalidDataException("Некорректный Content-Length.");

        if (contentLength < 0)
            throw new InvalidDataException("Отрицательный Content-Length.");

        var segments = rawPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(UnescapePath)
            .ToArray();

        var path = "/" + string.Join('/', segments);
        var query = ParseQuery(rawQuery);

        return new HttpRequest
        {
            Method = method,
            Path = path,
            Segments = segments,
            Query = query,
            Headers = headers,
            Body = new RequestBodyStream(prefix, stream, contentLength),
            ContentLength = contentLength
        };
    }

    public static Task WriteJsonAsync(Stream stream, int status, object payload, CancellationToken cancellationToken) =>
        WriteBytesAsync(
            stream,
            status,
            "application/json; charset=utf-8",
            JsonSerializer.SerializeToUtf8Bytes(payload, ProtocolJson.Options),
            cancellationToken);

    public static Task WriteErrorAsync(Stream stream, int status, string message, CancellationToken cancellationToken) =>
        WriteJsonAsync(stream, status, new { error = message }, cancellationToken);

    public static async Task WriteBytesAsync(
        Stream stream,
        int status,
        string contentType,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        var head = Head(status, contentType, body.Length);
        await stream.WriteAsync(head, cancellationToken);
        if (body.Length > 0)
            await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task WriteStreamAsync(
        Stream stream,
        int status,
        string contentType,
        long length,
        Stream source,
        CancellationToken cancellationToken)
    {
        var head = Head(status, contentType, length);
        await stream.WriteAsync(head, cancellationToken);
        await source.CopyToAsync(stream, 64 * 1024, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static Task WriteEmptyAsync(Stream stream, int status, CancellationToken cancellationToken) =>
        WriteBytesAsync(stream, status, "text/plain; charset=utf-8", ReadOnlyMemory<byte>.Empty, cancellationToken);

    /// <summary>Стартовые заголовки SSE: тело не ограничено, соединение держится открытым.</summary>
    public static async Task WriteSseHeadersAsync(Stream stream, CancellationToken cancellationToken)
    {
        const string head =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/event-stream; charset=utf-8\r\n" +
            "Cache-Control: no-cache, no-store\r\n" +
            "Connection: keep-alive\r\n" +
            "X-Accel-Buffering: no\r\n" +
            "\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static Task WriteSseEventAsync(Stream stream, string eventName, string data, CancellationToken cancellationToken)
    {
        var payload = $"event: {eventName}\ndata: {data}\n\n";
        return WriteRawAsync(stream, payload, cancellationToken);
    }

    public static Task WriteSseCommentAsync(Stream stream, CancellationToken cancellationToken) =>
        WriteRawAsync(stream, ": ping\n\n", cancellationToken);

    private static async Task WriteRawAsync(Stream stream, string text, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static byte[] Head(int status, string contentType, long length)
    {
        var head =
            $"HTTP/1.1 {status} {StatusText(status)}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "X-Content-Type-Options: nosniff\r\n" +
            "Connection: close\r\n" +
            "\r\n";

        return Encoding.ASCII.GetBytes(head);
    }

    private static string StatusText(int status) => status switch
    {
        200 => "OK",
        204 => "No Content",
        400 => "Bad Request",
        404 => "Not Found",
        405 => "Method Not Allowed",
        409 => "Conflict",
        411 => "Length Required",
        413 => "Payload Too Large",
        415 => "Unsupported Media Type",
        500 => "Internal Server Error",
        _ => "OK"
    };

    private static int FindHeaderEnd(List<byte> buffer)
    {
        for (var index = 3; index < buffer.Count; index++)
        {
            if (buffer[index - 3] == '\r' && buffer[index - 2] == '\n' &&
                buffer[index - 1] == '\r' && buffer[index] == '\n')
                return index - 3;
        }

        return -1;
    }

    private static Dictionary<string, string> ParseQuery(string rawQuery)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(rawQuery))
            return result;

        foreach (var pair in rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            var key = equals >= 0 ? pair[..equals] : pair;
            var value = equals >= 0 ? pair[(equals + 1)..] : string.Empty;
            result[UnescapeQuery(key)] = UnescapeQuery(value);
        }

        return result;
    }

    private static string UnescapePath(string value) => Uri.UnescapeDataString(value);

    private static string UnescapeQuery(string value) =>
        Uri.UnescapeDataString(value.Replace('+', ' '));
}
