using System.Text;
using DnDMap.Core.Storage;
using Xunit;

namespace DnDMap.Core.Tests.Storage;

public sealed class PhysicalFileStoreTests : IDisposable
{
    private readonly string _root;
    private readonly PhysicalFileStore _store;

    public PhysicalFileStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dndmap-tests-" + Guid.NewGuid().ToString("N"));
        _store = new PhysicalFileStore(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Constructor_CreatesKindFolders()
    {
        Assert.True(Directory.Exists(Path.Combine(_root, "maps")));
        Assert.True(Directory.Exists(Path.Combine(_root, "tokens")));
    }

    [Fact]
    public void Write_Read_Delete_Roundtrip()
    {
        _store.Write(FileKind.Map, "map.png", Bytes("hello"));

        Assert.True(_store.Exists(FileKind.Map, "map.png"));

        using (var stream = _store.OpenRead(FileKind.Map, "map.png"))
        using (var reader = new StreamReader(stream))
            Assert.Equal("hello", reader.ReadToEnd());

        Assert.True(_store.Delete(FileKind.Map, "map.png"));
        Assert.False(_store.Exists(FileKind.Map, "map.png"));
        Assert.False(_store.Delete(FileKind.Map, "map.png"));
    }

    [Fact]
    public void List_FiltersByExtension()
    {
        _store.Write(FileKind.Map, "a.png", Bytes("a"));
        _store.Write(FileKind.Map, "b.JPG", Bytes("b"));
        _store.Write(FileKind.Map, "notes.txt", Bytes("c"));

        var entries = _store.List(FileKind.Map);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, entry => entry.Name == "a.png");
        Assert.Contains(entries, entry => entry.Name == "b.JPG");
    }

    [Fact]
    public void List_KeepsKindsSeparate()
    {
        _store.Write(FileKind.Map, "shared.png", Bytes("map"));
        _store.Write(FileKind.Token, "goblin.png", Bytes("token"));

        Assert.Single(_store.List(FileKind.Map));
        Assert.Single(_store.List(FileKind.Token));
    }

    [Theory]
    [InlineData("../x.png")]
    [InlineData("a/b.png")]
    [InlineData("a\\b.png")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData("bad\u0001name.png")]
    [InlineData("CON.png")]
    [InlineData("nul.jpg")]
    public void ValidateName_Rejects_Invalid(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => _store.ValidateName(name));
    }

    [Fact]
    public void ValidateName_Rejects_TooLong()
    {
        Assert.ThrowsAny<ArgumentException>(() => _store.ValidateName(new string('a', 121)));
    }

    [Fact]
    public void ValidateName_AcceptsOrdinaryNames()
    {
        Assert.Equal("goblin_01.png", _store.ValidateName("goblin_01.png"));
        Assert.Equal("map-v2.final.jpg", _store.ValidateName("map-v2.final.jpg"));
    }

    [Fact]
    public void ValidateName_AcceptsLaptopNames()
    {
        // Имена с ноутбука: пробелы и кириллица — обычное дело (M2).
        Assert.Equal("with space.png", _store.ValidateName("with space.png"));
        Assert.Equal("Карта подземелья.png", _store.ValidateName("Карта подземелья.png"));
        Assert.Equal("Гоблин (синий).png", _store.ValidateName("Гоблин (синий).png"));
    }
}
