using System.Buffers.Binary;

namespace DnDMap.Core.Imaging;

/// <summary>
/// Читает размеры PNG и JPEG из заголовка файла, без System.Drawing
/// (на Android его нет). Используется при загрузке карты/токена на хост.
/// </summary>
public static class ImageSizeReader
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Читает размеры изображения из потока. Бросает InvalidDataException на неподдерживаемый файл.</summary>
    public static (int Width, int Height) Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var data = ReadHeader(stream);
        return Read(data);
    }

    /// <summary>Пробует прочитать размеры; на неподдерживаемом/битом файле возвращает false.</summary>
    public static bool TryRead(Stream stream, out (int Width, int Height) size)
    {
        try
        {
            size = Read(stream);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentNullException)
        {
            size = default;
            return false;
        }
    }

    /// <summary>Читает размеры из заголовка, уже загруженного в память.</summary>
    public static (int Width, int Height) Read(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 24 && data[..8].SequenceEqual(PngSignature))
            return ReadPng(data);

        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8)
            return ReadJpeg(data);

        throw new InvalidDataException("Неизвестный формат изображения (поддерживаются PNG и JPEG).");
    }

    private static byte[] ReadHeader(Stream stream)
    {
        // 64 КБ с запасом: JPEG с большим EXIF тоже помещается.
        const int maxHeader = 64 * 1024;

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while (buffer.Length < maxHeader && (read = stream.Read(chunk, 0, chunk.Length)) > 0)
            buffer.Write(chunk, 0, read);

        return buffer.ToArray();
    }

    private static (int Width, int Height) ReadPng(ReadOnlySpan<byte> data)
    {
        // Сигнатура (8) + длина чанка (4) + "IHDR" (4) + width (4) + height (4).
        var width = BinaryPrimitives.ReadInt32BigEndian(data.Slice(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(20, 4));

        if (width <= 0 || height <= 0)
            throw new InvalidDataException("Некорректный PNG: нулевые размеры.");

        return (width, height);
    }

    private static (int Width, int Height) ReadJpeg(ReadOnlySpan<byte> data)
    {
        var index = 2; // пропускаем SOI (FF D8)

        while (index + 3 < data.Length)
        {
            if (data[index] != 0xFF)
            {
                index++;
                continue;
            }

            var marker = data[index + 1];
            index += 2;

            // Маркеры без сегмента данных.
            if (marker is 0xD8 or 0xD9 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                continue;

            if (index + 1 >= data.Length)
                break;

            var length = (data[index] << 8) | data[index + 1];
            if (length < 2)
                break;

            var isStartOfFrame = marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
            if (isStartOfFrame)
            {
                if (index + 6 >= data.Length)
                    break;

                var height = (data[index + 3] << 8) | data[index + 4];
                var width = (data[index + 5] << 8) | data[index + 6];

                if (width > 0 && height > 0)
                    return (width, height);

                break;
            }

            index += length;
        }

        throw new InvalidDataException("Не удалось найти SOF-маркер в JPEG.");
    }
}
