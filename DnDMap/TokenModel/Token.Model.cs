using DnDMap.Core;
using DnDMap.Core.BaseClasses;
using DnDMap.Core.Enums;

namespace DnDMap.TokenModel;

public partial class Token : ViewModelBase
{
    #region Properties

    public int Id { get; set; }

    public string ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public double X
    {
        get => _x;
        private set => SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        private set => SetProperty(ref _y, value);
    }

    public double Diameter => GlobalSettings.TokenDiameter * (int)EntitySize;

    public EntitySize EntitySize
    {
        get => _entitySize;
        set
        {
            if (SetProperty(ref _entitySize, value))
                OnPropertyChanged(nameof(Diameter));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    #endregion

    public Token(string imagePath, EntitySize entitySize)
    {
        ImagePath = imagePath;
        EntitySize = entitySize;
    }

    #region Fields

    private double _x;
    private double _y;
    private EntitySize _entitySize;
    private string _imagePath;
    private bool _isSelected;

    #endregion
}