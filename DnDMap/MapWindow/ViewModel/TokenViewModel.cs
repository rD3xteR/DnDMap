using DnDMap.Core.BaseClasses;

namespace DnDMap.MapWindow.ViewModel;

/// <summary>
/// Экранное представление токена ядра: координаты и диаметр уже в пикселях окна.
/// </summary>
public sealed class TokenViewModel(string id, string file, string? imagePath) : ViewModelBase
{
    private string? _imagePath = imagePath;

    public string Id { get; } = id;

    /// <summary>Имя файла в библиотеке токенов (как хранит ядро).</summary>
    public string File { get; } = file;

    /// <summary>
    /// Абсолютный путь к картинке — известен только WPF. Присваивается после
    /// создания: путь добавляется в словарь уже после события сессии.
    /// </summary>
    public string? ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public double Left { get; set => SetProperty(ref field, value); }

    public double Top { get; set => SetProperty(ref field, value); }

    public double Diameter { get; set => SetProperty(ref field, value); }

    public bool IsSelected { get; set => SetProperty(ref field, value); }
}
