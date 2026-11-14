using DnDMap.Core.Imaging;
using Xunit;

namespace DnDMap.Core.Tests.Imaging;

public sealed class GridMapRendererTests
{
    [Fact]
    public void Render_ProducesPngWithExpectedSize()
    {
        var png = GridMapRenderer.Render(15, 10);

        Assert.True(ImageSizeReader.TryRead(new MemoryStream(png), out var size));
        Assert.Equal(1500, size.Width);
        Assert.Equal(1000, size.Height);

        // PNG: сигнатура в начале и завершающий IEND.
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(signature, png.Take(8).ToArray());
        Assert.Equal("IEND", System.Text.Encoding.ASCII.GetString(png, png.Length - 8, 4));
    }

    [Theory]
    [InlineData(9, 10)]
    [InlineData(31, 10)]
    [InlineData(15, 4)]
    [InlineData(15, 21)]
    public void Render_RejectsOutOfRange(int columns, int rows)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridMapRenderer.Render(columns, rows));
    }

    [Fact]
    public void SizeOf_MatchesCellGrid()
    {
        Assert.Equal((3000, 2000), GridMapRenderer.SizeOf(30, 20));
    }
}
