using DnDMap.TokenModel;
using System.Collections.ObjectModel;
using Point = System.Windows.Point;

namespace DnDMap.MapWindow.ViewModel
{
    public partial class MapViewModel
    {
        #region Properties

        public string? MapPath
        {
            get => _mapPath;
            set => SetProperty(ref _mapPath, value);
        }

        public Point? SelectedTokenOffset { get; set; }

        public Token? SelectedToken
        {
            get => _selectedToken;
            set => SetProperty(ref _selectedToken, value);
        }

        public ObservableCollection<Token> Tokens
        {
            get => _tokens;
            set => SetProperty(ref _tokens, value);
        }

        #endregion

        #region Fields

        private string? _mapPath;
        private Token? _selectedToken;
        private ObservableCollection<Token> _tokens = new();
        private MapWindow _window;

        #endregion
    }
}
