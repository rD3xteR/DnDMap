using DnDMap.Core;
using DnDMap.Models;
using System.IO;
using System.Windows.Input;

namespace DnDMap.SettingsWindow.ViewModel
{
    public partial class SettingsViewModel
    {
        public ICommand BrowseMapsCommand { get; }
        public ICommand BrowseTokensCommand { get; }
        public ICommand OpenMapCommand { get; }
        public ICommand AddTokenCommand { get; }

        public void OnClosing()
        {
            Paths.Default.MapsPath = MapsPath;
            Paths.Default.TokensPath = TokensPath;

            Paths.Default.Save();
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

            _mapViewModel.AddToken(new Core.Parameters.TokenParams((Core.Enums.EntitySize)SelectedEntitySize, SelectedToken.Path));
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
                    .Where(file => GlobalSettings.SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
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
