using Point = System.Windows.Point;

namespace DnDMap.TokenModel
{
    public partial class Token
    {
        public Point GetOffset(Point point) =>
            new(point.X - X, point.Y - Y);

        public void Move(Point point, Point offset)
        {
            X = point.X - offset.X;
            Y = point.Y - offset.Y;
        }

        public void OnGlobalTokenSizeChanged()
        {
            OnPropertyChanged(nameof(Diameter));
        }
    }
}
