using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace DnDMap.Core.Imaging;

/// <summary>
/// Пустая карта-сетка: ровные клетки на светлом фоне, без внешних библиотек.
/// PNG пишется вручную, потому что System.Drawing недоступен в BCL и на Android.
/// </summary>
public static class GridMapRenderer
{
    public const int CellSizePx = 100;
    public const int MinColumns = 10;
    public const int MaxColumns = 30;
    public const int MinRows = 5;
    public const int MaxRows = 20;

    private static readonly (byte R, byte G, byte B) Background = (0xF3, 0xEC, 0xD9);
    private static readonly (byte R, byte G, byte B) Grid = (0x9C, 0x8F, 0x74);
    private static readonly (byte R, byte G, byte B) Border = (0x5F, 0x55, 0x40);

    private const int GridThickness = 2;
    private const int BorderThickness = 3;

    /// <summary>Размер карты в пикселях для заданного числа клеток.</summary>
    public static (int Width, int Height) SizeOf(int columns, int rows) =>
        (columns * CellSizePx, rows * CellSizePx);

    /// <summary>PNG с сеткой. Бросает <see cref="ArgumentOutOfRangeException"/> вне допустимых границ.</summary>
    public static byte[] Render(int columns, int rows)
    {
        if (columns < MinColumns || columns > MaxColumns)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columns), columns, $"Клеток по ширине: {MinColumns}..{MaxColumns}.");
        }

        if (rows < MinRows || rows > MaxRows)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rows), rows, $"Клеток по высоте: {MinRows}..{MaxRows}.");
        }

        var (width, height) = SizeOf(columns, rows);

        using var output = new MemoryStream();
        WritePng(output, width, height, (y, row) => FillRow(y, row, width, height));
        return output.ToArray();
    }

    private static void FillRow(int y, Span<byte> row, int width, int height)
    {
        var borderRow = y < BorderThickness || y >= height - BorderThickness;
        var gridRow = y % CellSizePx < GridThickness;

        for (var x = 0; x < width; x++)
        {
            var border = borderRow || x < BorderThickness || x >= width - BorderThickness;
            var line = gridRow || x % CellSizePx < GridThickness;

            var color = border ? Border : line ? Grid : Background;
            var offset = x * 3;
            row[offset] = color.R;
            row[offset + 1] = color.G;
            row[offset + 2] = color.B;
        }
    }

    // ----- минимальный PNG-энкодер (truecolor, без альфы) -----

    private delegate void RowFiller(int y, Span<byte> row);

    private static void WritePng(Stream output, int width, int height, RowFiller fillRow)
    {
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;  // глубина 8 бит на канал
        header[9] = 2;  // truecolor RGB
        WriteChunk(output, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var row = new byte[1 + width * 3];

            for (var y = 0; y < height; y++)
            {
                row[0] = 0; // фильтр None
                fillRow(y, row.AsSpan(1));
                zlib.Write(row, 0, row.Length);
            }
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        output.Write(buffer);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);

        BinaryPrimitives.WriteUInt32BigEndian(buffer, Crc32(crcInput));
        output.Write(buffer);
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in data)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }
}
