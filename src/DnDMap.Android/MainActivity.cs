using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using DnDMap.Server;

namespace DnDMap.Host;

/// <summary>
/// Экран стола: поднимает HTTP-сервер на планшете и показывает карту во весь экран.
/// Пульт мастера открывается с ноутбука в браузере по адресу из Toast.
/// </summary>
[Activity(
    Label = "DnDMap",
    MainLauncher = true,
    Exported = true,
    Theme = "@android:style/Theme.Material.NoActionBar.Fullscreen",
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                           ConfigChanges.ScreenLayout | ConfigChanges.UiMode |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.KeyboardHidden)]
public sealed class MainActivity : Activity
{
    private const int Port = 5000;

    private MapServer? _server;
    private WebView? _view;
    private string? _cacheRoot;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Экран стола не должен гаснуть во время игры.
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);

        _cacheRoot = Path.Combine(FilesDir!.AbsolutePath, "session-cache");
        ResetCache(_cacheRoot);

        try
        {
            _server = StartServer(_cacheRoot);
        }
        catch (Exception error)
        {
            ShowFatal(error);
            return;
        }

        _view = new WebView(this);
        _view.SetBackgroundColor(global::Android.Graphics.Color.Black);

        var settings = _view.Settings;
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;

        _view.SetWebViewClient(new WebViewClient());
        SetContentView(_view);
        _view.LoadUrl($"http://127.0.0.1:{_server.Port}/?display=1");

        Toast.MakeText(this, $"DnDMap: http://{LocalAddress()}:{_server.Port}/", ToastLength.Long)?.Show();
    }

    protected override void OnDestroy()
    {
        _view?.StopLoading();
        _view?.Destroy();
        _view = null;

        var server = _server;
        _server = null;

        if (server is not null)
        {
            // Dispose внутри ждёт StopAsync; на UI-потоке это может заблокироваться.
            Task.Run(server.Dispose).Wait(TimeSpan.FromSeconds(3));
        }

        if (_cacheRoot is not null)
            ResetCache(_cacheRoot);

        base.OnDestroy();
    }

    /// <summary>Порт 5000 для предсказуемого адреса; если занят — любой свободный.</summary>
    private static MapServer StartServer(string root)
    {
        MapServer? server = null;

        try
        {
            server = CreateServer(root, Port);
            server.Start();
            return server;
        }
        catch (Exception)
        {
            server?.Dispose();
        }

        var fallback = CreateServer(root, 0);
        fallback.Start();
        return fallback;
    }

    private static MapServer CreateServer(string root, int port) =>
        new(new ServerOptions
        {
            Port = port,
            BindAddress = "0.0.0.0",
            StorageRoot = root
        })
        {
            Log = message => global::Android.Util.Log.Warn("DnDMap", message)
        };

    private void ShowFatal(Exception error)
    {
        global::Android.Util.Log.Error("DnDMap", error.ToString());

        var text = new TextView(this)
        {
            Text = $"Не удалось запустить сервер DnDMap:\n{error.Message}"
        };

        text.SetPadding(32, 32, 32, 32);
        SetContentView(text);
    }

    private static string LocalAddress()
    {
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up ||
                adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(unicast.Address))
                    return unicast.Address.ToString();
            }
        }

        return "127.0.0.1";
    }

    private static void ResetCache(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (Exception error)
        {
            global::Android.Util.Log.Warn("DnDMap", $"cache reset: {error.Message}");
        }

        Directory.CreateDirectory(root);
    }
}
