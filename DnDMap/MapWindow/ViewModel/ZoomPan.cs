using Point = System.Windows.Point;

namespace DnDMap.MapWindow.ViewModel
{
    public partial class MapViewModel
    {
        public void SetMapSize(double width, double height)
        {
            _mapWidth = width;
            _mapHeight = height;
            RecalcOffset();
        }

        public void SetViewportSize(double width, double height)
        {
            _viewportWidth = width;
            _viewportHeight = height;
            RecalcOffset();
        }

        private void RecalcOffset()
        {
            if (_mapWidth == 0 || _mapHeight == 0 || _viewportWidth == 0 || _viewportHeight == 0)
                return;

            double scaledWidth = _mapWidth * Zoom;
            double scaledHeight = _mapHeight * Zoom;

            // Горизонталь
            if (scaledWidth > _viewportWidth)
            {
                double minX = _viewportWidth - scaledWidth;
                double maxX = 0;
                OffsetX = Math.Max(minX, Math.Min(maxX, OffsetX));
            }
            else
            {
                OffsetX = (_viewportWidth - scaledWidth) / 2;
            }

            // Вертикаль
            if (scaledHeight > _viewportHeight)
            {
                double minY = _viewportHeight - scaledHeight;
                double maxY = 0;
                OffsetY = Math.Max(minY, Math.Min(maxY, OffsetY));
            }
            else
            {
                OffsetY = (_viewportHeight - scaledHeight) / 2;
            }
        }

        public (double appliedX, double appliedY) Pan(double deltaX, double deltaY)
        {
            double oldX = OffsetX;
            double oldY = OffsetY;

            double scaledWidth = _mapWidth * Zoom;
            double scaledHeight = _mapHeight * Zoom;

            if (scaledWidth > _viewportWidth)
            {
                double newX = OffsetX + deltaX;
                double minX = _viewportWidth - scaledWidth;
                double maxX = 0;
                OffsetX = Math.Max(minX, Math.Min(maxX, newX));
            }
            else
            {
                OffsetX = (_viewportWidth - scaledWidth) / 2;
            }

            if (scaledHeight > _viewportHeight)
            {
                double newY = OffsetY + deltaY;
                double minY = _viewportHeight - scaledHeight;
                double maxY = 0;
                OffsetY = Math.Max(minY, Math.Min(maxY, newY));
            }
            else
            {
                OffsetY = (_viewportHeight - scaledHeight) / 2;
            }

            return (OffsetX - oldX, OffsetY - oldY);
        }

        public void ZoomAtPoint(Point canvasPoint, double newZoom)
        {
            double oldZoom = Zoom;
            newZoom = Math.Max(1.0, Math.Min(5.0, newZoom));
            if (Math.Abs(newZoom - oldZoom) < 0.001) return;

            double dx = canvasPoint.X - (canvasPoint.X * (newZoom / oldZoom));
            double dy = canvasPoint.Y - (canvasPoint.Y * (newZoom / oldZoom));

            Zoom = newZoom;

            double newX = OffsetX + dx;
            double newY = OffsetY + dy;

            double scaledWidth = _mapWidth * Zoom;
            double scaledHeight = _mapHeight * Zoom;

            if (scaledWidth > _viewportWidth)
            {
                double minX = _viewportWidth - scaledWidth;
                double maxX = 0;
                OffsetX = Math.Max(minX, Math.Min(maxX, newX));
            }
            else
            {
                OffsetX = (_viewportWidth - scaledWidth) / 2;
            }

            if (scaledHeight > _viewportHeight)
            {
                double minY = _viewportHeight - scaledHeight;
                double maxY = 0;
                OffsetY = Math.Max(minY, Math.Min(maxY, newY));
            }
            else
            {
                OffsetY = (_viewportHeight - scaledHeight) / 2;
            }
        }
    }
}