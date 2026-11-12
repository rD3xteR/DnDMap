using System.Security.Cryptography;
using DnDMap.Core.Storage;

namespace DnDMap.Server;

/// <summary>
/// SHA-256 файлов для манифеста. Кэш живёт до изменения файла через сервер:
/// загрузка/удаление инвалидируют запись, хэш повторно не считается.
/// </summary>
internal sealed class FileHashCache(IFileStore store)
{
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);

    public string Get(FileKind kind, string name)
    {
        var key = Key(kind, name);

        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;
        }

        using var stream = store.OpenRead(kind, name);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

        lock (_cache)
            _cache[key] = hash;

        return hash;
    }

    public void Invalidate(FileKind kind, string name)
    {
        lock (_cache)
            _cache.Remove(Key(kind, name));
    }

    private static string Key(FileKind kind, string name) => $"{kind}:{name}";
}
