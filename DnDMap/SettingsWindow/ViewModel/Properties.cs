using DnDMap.Core.Model;
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
            get => _mapViewModel.ReferenceDiameter;
            set
            {
                var rounded = Math.Round(value, 0);
                if (Math.Abs(_mapViewModel.ReferenceDiameter - rounded) < 1e-9)
                    return;

                _mapViewModel.SetDiameter(rounded);
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
