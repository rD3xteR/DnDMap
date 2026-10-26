using DnDMap.TokenModel;
using System.Collections.ObjectModel;
using Point = System.Windows.Point;

namespace DnDMap.MapWindow.ViewModel
{
    public partial class MapViewModel
    {
        #region Properties

        public string? MapPath { get; set => SetProperty(ref field, value); }
        public Token? SelectedToken { get; set => SetProperty(ref field, value); }
        public ObservableCollection<Token> Tokens { get; set => SetProperty(ref field, value); } = [];

        public double Zoom { get; set => SetProperty(ref field, Math.Max(1, Math.Min(3, value))); } = 1.0;
        public double OffsetX { get; set => SetProperty(ref field, value); } = 0;
        public double OffsetY { get; set => SetProperty(ref field, value); } = 0;

        public Point? SelectedTokenOffset { get; set; }

        #endregion


        #region Fields

        private MapWindow _window;
        private double _mapWidth, _mapHeight;
        private double _viewportWidth, _viewportHeight;

        #endregion
    }
}
