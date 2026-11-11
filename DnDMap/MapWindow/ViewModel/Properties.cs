using System.Collections.ObjectModel;
using System.Windows.Media;

namespace DnDMap.MapWindow.ViewModel;

public partial class MapViewModel
{
    #region Properties

    /// <summary>Полный путь к открытой карте (нужен только WPF, в ядро не попадает).</summary>
    public string? MapPath { get; private set => SetProperty(ref field, value); }

    public ImageSource? MapImageSource { get; private set => SetProperty(ref field, value); }

    /// <summary>Токены в экранных координатах — пересобираются из снимка сессии.</summary>
    public ObservableCollection<TokenViewModel> Tokens { get; } = [];

    public string? SelectedTokenId { get; private set => SetProperty(ref field, value); }

    /// <summary>Зум относительно вписанной карты (1..3), значение сессии.</summary>
    public double Zoom => _session.View.Zoom;

    /// <summary>Базовый диаметр токена в пикселях эталонного экрана 1920×1080.</summary>
    public double ReferenceDiameter { get; private set; }

    // Положение карты на экране в пикселях окна.
    public double MapLeft { get; private set => SetProperty(ref field, value); }

    public double MapTop { get; private set => SetProperty(ref field, value); }

    public double MapWidth { get; private set => SetProperty(ref field, value); }

    public double MapHeight { get; private set => SetProperty(ref field, value); }

    #endregion

    #region Fields

    private MapWindow? _window;

    #endregion
}
