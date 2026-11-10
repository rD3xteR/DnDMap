using DnDMap.Core.Imaging;
using Xunit;

namespace DnDMap.Core.Tests.Imaging;

public class ImageSizeReaderTests
{
    private static byte[] PngHeader(int width, int height)
    {
        var data = new byte[24];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(data, 0);

        data[11] = 13; // длина IHDR
        data[12] = (byte)'I';
        data[13] = (byte)'H';
        data[14] = (byte)'D';
        data[15] = (byte)'R';

        WriteInt32BigEndian(data, 16, width);
        WriteInt32BigEndian(data, 20, height);
        return data;
    }

    private static byte[] JpegHeader(int width, int height) =>
    [
        0xFF, 0xD8,                      // SOI
        0xFF, 0xC0,                      // SOF0
        0x00, 0x11,                      // длина сегмента
        0x08,                            // точность
        (byte)(height >> 8), (byte)height,
        (byte)(width >> 8), (byte)width,
        0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01
    ];

    private static void WriteInt32BigEndian(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }

    [Fact]
    public void Read_Png_ReturnsSize()
    {
        var size = ImageSizeReader.Read(PngHeader(1600, 900));

        Assert.Equal(1600, size.Width);
        Assert.Equal(900, size.Height);
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(7680, 4320)]
    public void Read_Jpeg_ReturnsSize(int width, int height)
    {
        var size = ImageSizeReader.Read(JpegHeader(width, height));

        Assert.Equal(width, size.Width);
        Assert.Equal(height, size.Height);
    }

    [Fact]
    public void Read_FromStream_Works()
    {
        using var stream = new MemoryStream(PngHeader(1920, 1080));

        var size = ImageSizeReader.Read(stream);

        Assert.Equal(1920, size.Width);
        Assert.Equal(1080, size.Height);
    }

    [Fact]
    public void Read_BrokenFile_Throws()
    {
        Assert.Throws<InvalidDataException>(() => ImageSizeReader.Read(new MemoryStream([1, 2, 3, 4])));
    }

    [Fact]
    public void TryRead_OnGarbage_ReturnsFalse()
    {
        Assert.False(ImageSizeReader.TryRead(new MemoryStream([1, 2, 3, 4]), out _));
    }

    [Fact]
    public void TryRead_OnValidPng_ReturnsTrue()
    {
        Assert.True(ImageSizeReader.TryRead(new MemoryStream(PngHeader(10, 20)), out var size));
        Assert.Equal(10, size.Width);
        Assert.Equal(20, size.Height);
    }
}
