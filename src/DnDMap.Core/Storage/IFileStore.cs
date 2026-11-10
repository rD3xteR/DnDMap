namespace DnDMap.Core.Storage;

/// <summary>
/// Абстракция хранилища карт и токенов. Реализации: обычная папка (Windows)
/// и внутренняя папка приложения (Android) — обе работают через System.IO.
/// </summary>
public interface IFileStore
{
    IReadOnlyList<FileEntry> List(FileKind kind);

    bool Exists(FileKind kind, string name);

    Stream OpenRead(FileKind kind, string name);

    void Write(FileKind kind, string name, Stream content);

    bool Delete(FileKind kind, string name);

    /// <summary>Проверяет имя файла и возвращает его же. Бросает ArgumentException на мусор.</summary>
    string ValidateName(string name);
}
