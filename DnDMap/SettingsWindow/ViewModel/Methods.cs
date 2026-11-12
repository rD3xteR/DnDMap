using DnDMap.Core.Storage;
using DnDMap.Models;
using DnDMap.Server;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Input;

namespace DnDMap.SettingsWindow.ViewModel
{
    public partial class SettingsViewModel
    {
        public ICommand BrowseMapsCommand { get; }
        public ICommand BrowseTokensCommand { get; }
        public ICommand OpenMapCommand { get; }
        public ICommand AddTokenCommand { get; }
        public ICommand ToggleServerCommand { get; }

        public void OnClosing()
        {
            StopServer();

            Paths.Default.MapsPath = MapsPath;
            Paths.Default.TokensPath = TokensPath;

            Paths.Default.Save();
        }

        // ----- сервер для планшета -----

        private void ToggleServer()
        {
            if (_server is not null)
                StopServer();
            else
                StartServer();
        }

        private void StartServer()
        {
            var port = Math.Clamp(ServerPort, 0, 65535);
            var root = SessionCachePath;
            MapServer? server = null;

            try
            {
                // Кэш сессии живёт только на время игры: на старте он всегда чистый.
                ClearCache(root);

                server = new MapServer(new ServerOptions
                {
                    Port = port,
                    BindAddress = "0.0.0.0",
                    StorageRoot = root
                })
                {
                    Log = message => System.Diagnostics.Debug.WriteLine($"[server] {message}")
                };

                server.Start();

                _server = server;
                ServerPort = server.Port;
                ServerUrl = $"http://{LocalAddress()}:{server.Port}/";
                ServerStatus = $"Сервер запущен. Порт {server.Port}. Откройте адрес в браузере на ноутбуке.";
                IsServerRunning = true;
            }
            catch (Exception ex) when (ex is SocketException or IOException or UnauthorizedAccessException)
            {
                server?.Dispose();
                ServerStatus = $"Не удалось запустить сервер: {ex.Message}";
                ServerUrl = string.Empty;
                IsServerRunning = false;
            }
        }

        private void StopServer()
        {
            if (_server is null)
                return;

            _server.Dispose();
            _server = null;

            ServerUrl = string.Empty;
            ServerStatus = "Сервер остановлен";
            IsServerRunning = false;
        }

        /// <summary>%LOCALAPPDATA%\DnDMap\session-cache — временный кэш карт и токенов.</summary>
        public static string SessionCachePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DnDMap",
            "session-cache");

        private static void ClearCache(string root)
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Занятый файл не должен мешать запуску: PhysicalFileStore создаст папку сам.
                System.Diagnostics.Debug.WriteLine($"[server] cache cleanup: {ex.Message}");
            }

            Directory.CreateDirectory(root);
        }

        /// <summary>Адрес в локальной сети: сначала приватные диапазоны, иначе первый IPv4.</summary>
        private static string LocalAddress()
        {
            var addresses = NetworkInterface.GetAllNetworkInterfaces()
                .Where(net => net.OperationalStatus == OperationalStatus.Up &&
                              net.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(net => net.GetIPProperties().UnicastAddresses)
                .Select(unicast => unicast.Address)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Where(address => !IPAddress.IsLoopback(address))
                .ToList();

            var preferred = addresses.FirstOrDefault(IsPrivateAddress)
                            ?? addresses.FirstOrDefault();

            return preferred?.ToString() ?? "127.0.0.1";
        }

        private static bool IsPrivateAddress(IPAddress address)
        {
            var bytes = address.GetAddressBytes();

            return bytes[0] == 10
                   || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                   || (bytes[0] == 192 && bytes[1] == 168);
        }

        private void BrowseMaps()
        {
            var directory = GetDirectory("maps");
            if (directory is null) return;

            MapsPath = directory;
            MapItems = [];
            SetMaps();
        }

        private void SetMaps()
        {
            if (MapsPath is null) return;

            var maps = GetImages(MapsPath);
            maps.ForEach(MapItems.Add);
        }

        private void BrowseTokens()
        {
            var directory = GetDirectory("tokens");
            if (directory is null) return;

            TokensPath = directory;
            TokenItems = [];
            SetTokens();
        }

        private void SetTokens()
        {
            if (TokensPath is null) return;

            var tokens = GetImages(TokensPath);
            tokens.ForEach(TokenItems.Add);
        }

        private void AddToken()
        {
            if (SelectedToken is null || SelectedEntitySize is null) return;

            _mapViewModel.AddToken(SelectedToken.Path, SelectedEntitySize.Value);
        }

        private void OpenMap()
        {
            if (SelectedMap is null) return;
            var path = SelectedMap.Path;
            _mapViewModel.OpenWindow(path);
        }

        /// <summary>
        /// Get path to directory with images
        /// </summary>
        private string? GetDirectory(string imageType)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = $"Select folder with {imageType}",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return null;

            return dialog.SelectedPath;
        }

        /// <summary>
        /// Get images for specified directory
        /// </summary>
        private List<ImageItem> GetImages(string directory)
        {
            var images = new List<ImageItem>();

            try
            {
                var imageFiles = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => PhysicalFileStore.SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .Select(f => new ImageItem(Path.GetFileName(f), f))
                    .ToList();

                images.AddRange(imageFiles);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occured while loading images: {ex.Message} {ex.StackTrace}");
            }

            return images;
        }
    }
}
