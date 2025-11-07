using DnDMap.Core.Extensions;
using Point = System.Windows.Point;

namespace DnDMap.TokenModel
{
    public partial class Token
    {
        public bool PointAboveToken(Point point)
        {
            var inVertical = point.Y.Between(Y, Y + Diameter);
            var inHorizontal = point.X.Between(X, X + Diameter);

            return inVertical && inHorizontal;
        }

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
