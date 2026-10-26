using DnDMap.Core;
using DnDMap.Core.Enums;
using DnDMap.MapWindow.ViewModel;
using DnDMap.Models;
using System.Collections.ObjectModel;

namespace DnDMap.SettingsWindow.ViewModel
{
    public partial class SettingsViewModel
    {
        #region Properties

        public double DiameterToChange
        {
            get => GlobalSettings.TokenDiameter;
            set
            {
                if (GlobalSettings.TokenDiameter == value)
                    return;

                GlobalSettings.TokenDiameter = Math.Round(value, 0);
                OnPropertyChanged();
            }
        }

        public string? TokensPath { get; set => SetProperty(ref field, value); } = Paths.Default.TokensPath;

        public string? MapsPath { get; set => SetProperty(ref field, value); } = Paths.Default.MapsPath;

        public ObservableCollection<ImageItem> TokenItems { get; set => SetProperty(ref field, value); } = [];

        public ObservableCollection<ImageItem> MapItems { get; set => SetProperty(ref field, value); } = [];

        public ImageItem? SelectedMap { get; set => SetProperty(ref field, value); }

        public ImageItem? SelectedToken { get; set => SetProperty(ref field, value); }

        public EntitySize? SelectedEntitySize { get; set => SetProperty(ref field, value); } = EntitySize.Normal;

        #endregion

        #region Fields

        private MapViewModel _mapViewModel = new();

        #endregion
    }
}
