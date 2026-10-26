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
        _window = new();
        _window.DataContext = this;
        MapPath = mapPath;
        _window.Show();

        var mapImage = new BitmapImage(new Uri(mapPath));
        SetMapSize(mapImage.PixelWidth, mapImage.PixelHeight);
    }

    public void OnClosing()
    {
        Tokens.Clear();
        SelectedToken = null;
        SelectedTokenOffset = null;
        MapPath = null;
    }
}