using System.Reflection;

namespace DnDMap.Server.Http;

/// <summary>
/// Статика SPA лежит внутри сборки: на планшете нет ни интернета, ни внешних CDN.
/// Имена ресурсов заданы явно в csproj (LogicalName).
/// </summary>
internal static class EmbeddedAssets
{
    private static readonly Dictionary<string, (string Resource, string ContentType)> Assets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["/"] = ("DnDMap.Server.wwwroot.index.html", "text/html; charset=utf-8"),
            ["/index.html"] = ("DnDMap.Server.wwwroot.index.html", "text/html; charset=utf-8"),
            ["/app.css"] = ("DnDMap.Server.wwwroot.app.css", "text/css; charset=utf-8"),
            ["/app.js"] = ("DnDMap.Server.wwwroot.app.js", "text/javascript; charset=utf-8")
        };

    public static bool TryOpen(string path, out Stream stream, out string contentType)
    {
        if (!Assets.TryGetValue(path, out var asset))
        {
            stream = Stream.Null;
            contentType = string.Empty;
            return false;
        }

        var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(asset.Resource);
        if (resource is null)
        {
            stream = Stream.Null;
            contentType = string.Empty;
            return false;
        }

        stream = resource;
        contentType = asset.ContentType;
        return true;
    }
}
