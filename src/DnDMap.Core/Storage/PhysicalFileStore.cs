using System.Text.RegularExpressions;

namespace DnDMap.Core.Storage;

/// <summary>Хранилище на обычной папке: {root}/maps и {root}/tokens.</summary>
public sealed class PhysicalFileStore : IFileStore
{
    public static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg"];

    private static readonly Regex NamePattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    private readonly string _root;

    public PhysicalFileStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        _root = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(PathForKind(FileKind.Map));
        Directory.CreateDirectory(PathForKind(FileKind.Token));
    }

    public IReadOnlyList<FileEntry> List(FileKind kind)
    {
        var directory = PathForKind(kind);
        if (!Directory.Exists(directory))
            return [];

        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(file => SupportedExtensions.Contains(
                Path.GetExtension(file),
                StringComparer.OrdinalIgnoreCase))
            .Select(file => new FileEntry(Path.GetFileName(file), new FileInfo(file).Length))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool Exists(FileKind kind, string name) => File.Exists(PathForFile(kind, name));

    public Stream OpenRead(FileKind kind, string name) =>
        new FileStream(PathForFile(kind, name), FileMode.Open, FileAccess.Read, FileShare.Read);

    public void Write(FileKind kind, string name, Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var file = new FileStream(PathForFile(kind, name), FileMode.Create, FileAccess.Write, FileShare.None);
        content.CopyTo(file);
    }

    public bool Delete(FileKind kind, string name)
    {
        var path = PathForFile(kind, name);
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    public string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Length > 120)
            throw new ArgumentException("Имя файла слишком длинное (максимум 120 символов).", nameof(name));

        if (name is "." or "..")
            throw new ArgumentException("Недопустимое имя файла.", nameof(name));

        if (!NamePattern.IsMatch(name))
            throw new ArgumentException("Имя файла содержит недопустимые символы.", nameof(name));

        return name;
    }

    private string PathForKind(FileKind kind) => kind switch
    {
        FileKind.Map => Path.Combine(_root, "maps"),
        FileKind.Token => Path.Combine(_root, "tokens"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный вид файлов.")
    };

    private string PathForFile(FileKind kind, string name)
    {
        ValidateName(name);

        var directory = Path.GetFullPath(PathForKind(kind));
        var fullPath = Path.GetFullPath(Path.Combine(directory, name));

        // Защита от traversal: после нормализации путь обязан остаться внутри папки вида.
        if (!fullPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Путь выходит за пределы хранилища.", nameof(name));

        return fullPath;
    }
}
