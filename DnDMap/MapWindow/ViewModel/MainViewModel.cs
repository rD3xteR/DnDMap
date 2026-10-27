using DnDMap.Core;
using DnDMap.Core.BaseClasses;
using System.Windows.Media.Imaging;

namespace DnDMap.MapWindow.ViewModel;

public partial class MapViewModel : ViewModelBase
{
    public MapViewModel()
    {
        GlobalSettings.TokenDiameterChanged += OnGlobalTokenSizeChanged;
    }

    public void OpenWindow(string mapPath)
    {
        var mapImage = new BitmapImage(new Uri(mapPath));

        // Если окно уже открыто — переиспользуем его, а не создаём новое.
        // Токены при смене карты сохраняются.
        if (_window is { IsVisible: true })
        {
            MapPath = mapPath;
            SetMapSize(mapImage.PixelWidth, mapImage.PixelHeight);
            ResetView();
            _window.Activate();
            return;
        }

        _window = new();
        _window.DataContext = this;
        MapPath = mapPath;
        SetMapSize(mapImage.PixelWidth, mapImage.PixelHeight);
        _window.Show();
    }

    public void OnClosing()
    {
        Tokens.Clear();
        SelectedToken = null;
        SelectedTokenOffset = null;
        MapPath = null;
    }
}