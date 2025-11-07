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

        private async void BrowseMapsAsync()
        {
            var directory = GetDirectory("maps");
            if (directory is null) return;

            MapsPath = directory;
            MapItems ??= [];
            var maps = await GetImages(MapsPath);

            maps.ForEach(map =>
            {
                MapItems.Add(map);
            });
        }

        private async void BrowseTokensAsync()
        {
            var directory = GetDirectory("tokens");
            if (directory is null) return;

            TokensPath = directory;
            TokenItems ??= [];
            var tokens = await GetImages(TokensPath);

            tokens.ForEach(token =>
            {
                TokenItems.Add(token);
            });
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
            _mapViewModel.OpenWindow("C:/Users/User/Pictures/Maps/map1.jpg");
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
        private async Task<List<ImageItem>> GetImages(string directory)
        {
            var images = new List<ImageItem>();

            await Task.Run(() =>
            {
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
            });

            return images;
        }
    }
}
