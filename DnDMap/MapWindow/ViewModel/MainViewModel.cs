using DnDMap.Core;
using DnDMap.Core.BaseClasses;
using DnDMap.Core.Command;
using Point = System.Windows.Point;

namespace DnDMap.MapWindow.ViewModel;

public partial class MapViewModel : ViewModelBase
{
    public MapViewModel()
    {
        #region Commands

        MouseDownCommand = new RelayCommand<Point>(OnMouseDown);
        MouseMoveCommand = new RelayCommand<Point>(OnMouseMove);

        #endregion

        GlobalSettings.TokenDiameterChanged += OnGlobalTokenSizeChanged;
    }
}