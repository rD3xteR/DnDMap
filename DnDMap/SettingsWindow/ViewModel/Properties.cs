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

        public string? TokensPath
        {
            get => _tokensPath;
            set => SetProperty(ref _tokensPath, value);
        }

        public string? MapsPath
        {
            get => _mapsPath;
            set => SetProperty(ref _mapsPath, value);
        }

        public ObservableCollection<ImageItem> TokenItems
        {
            get => _tokenItems;
            set => SetProperty(ref _tokenItems, value);
        }


        public ObservableCollection<ImageItem> MapItems
        {
            get => _mapItems;
            set => SetProperty(ref _mapItems, value);
        }

        public ImageItem? SelectedMap
        {
            get => _selectedMap;
            set => SetProperty(ref _selectedMap, value);
        }

        public ImageItem? SelectedToken
        {
            get => _selectedToken;
            set => SetProperty(ref _selectedToken, value);
        }

        public EntitySize? SelectedEntitySize
        {
            get => _selectedEntitySize;
            set => SetProperty(ref _selectedEntitySize, value);
        }

        #endregion

        #region Fields

        private double _diameterToChange;
        private string? _tokensPath;
        private string? _mapsPath;
        private ObservableCollection<ImageItem> _tokenItems = new();
        private ObservableCollection<ImageItem> _mapItems = new();
        private ImageItem? _selectedMap;
        private ImageItem? _selectedToken;
        private MapViewModel _mapViewModel = new();
        private EntitySize? _selectedEntitySize;

        #endregion
    }
}
