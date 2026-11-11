using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DnDMap.Core.BaseClasses;
using DnDMap.Core.Model;
using DnDMap.Core.Protocol;

namespace DnDMap.MapWindow.ViewModel;

/// <summary>
/// Мост между WPF и ядром: состояние живёт в <see cref="MapSession"/>, а
/// ViewModel только переводит его в экранные координаты и обратно.
/// </summary>
public partial class MapViewModel : ViewModelBase
{
    private readonly List<(string ImagePath, EntitySize Size)> _pendingTokens = [];
    private readonly Dictionary<string, string> _tokenImagePaths = new(StringComparer.Ordinal);

    private MapSession _session = new();

    public MapViewModel()
    {
        ReferenceDiameter = _session.Options.DefaultDiameterReferencePx;
        SubscribeSession();
    }

    public void OpenWindow(string mapPath)
    {
        OpenMap(mapPath);

        if (_window is { IsVisible: true })
        {
            _window.Activate();
            return;
        }

        _window = new();
        _window.DataContext = this;
        _window.Show();
    }

    /// <summary>
    /// Открыть карту без создания окна: состояние и экранная раскладка обновляются
    /// сразу — это позволяет проверять мост без WPF-окна.
    /// </summary>
    public void OpenMap(string mapPath)
    {
        var image = LoadImage(mapPath);

        if (!_session.Apply(new SetMapCommand(
                Path.GetFileName(mapPath),
                image.PixelWidth,
                image.PixelHeight)))
            return;

        MapPath = mapPath;
        MapImageSource = image;

        // Открытие карты сбрасывает диаметр к значению по умолчанию, а слайдер
        // живёт вне сессии — возвращаем выбранный пользователем размер.
        if (Math.Abs(ReferenceDiameter - _session.Options.DefaultDiameterReferencePx) > 1e-9)
            _session.Apply(new SetDiameterCommand(ReferenceDiameter));

        FlushPendingTokens();
    }

    public void OnClosing()
    {
        _pendingTokens.Clear();
        _tokenImagePaths.Clear();
        _selectedTokenId = null;
        _dragTokenId = null;

        UnsubscribeSession();
        _session = new MapSession();
        SubscribeSession();

        MapPath = null;
        MapImageSource = null;
        SelectedTokenId = null;
        Tokens.Clear();
        Refresh();
    }

    private void SubscribeSession() => _session.Changed += OnSessionChanged;

    private void UnsubscribeSession() => _session.Changed -= OnSessionChanged;

    private void OnSessionChanged(SessionChange change)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            Refresh();
        else
            dispatcher.BeginInvoke(Refresh);
    }

    /// <summary>
    /// Загружает картинку сразу в память (CacheOption.OnLoad), чтобы файл
    /// не оставался заблокированным приложением.
    /// </summary>
    private static BitmapImage LoadImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
