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

        /// <summary>
        /// Сброс зума и позиции: карта возвращается в масштаб 1× и центрируется в окне.
        /// </summary>
        public void ResetView()
        {
            Zoom = 1.0;
            OffsetX = 0;
            OffsetY = 0;
            RecalcOffset();
        }

        /// <summary>
        /// Размер, в котором карта реально рисуется на экране при текущем зуме.
        /// Карта отображается как фон Canvas через ImageBrush Stretch="Uniform",
        /// поэтому она вписывается в окно, а не рисуется в пиксельных размерах файла.
        /// </summary>
        private (double width, double height) GetScaledSize()
        {
            if (_mapWidth <= 0 || _mapHeight <= 0 || _viewportWidth <= 0 || _viewportHeight <= 0)
                return (0, 0);

            double fit = Math.Min(_viewportWidth / _mapWidth, _viewportHeight / _mapHeight);

            return (_mapWidth * fit * Zoom, _mapHeight * fit * Zoom);
        }

        /// <summary>
        /// Держит карту так, чтобы она покрывала экран: если после зума карта
        /// больше окна — не даёт утащить её за край; если меньше — центрирует.
        /// </summary>
        private void RecalcOffset()
        {
            var (scaledWidth, scaledHeight) = GetScaledSize();
            if (scaledWidth == 0 || scaledHeight == 0)
                return;

            // Горизонталь
            if (scaledWidth > _viewportWidth)
                OffsetX = Math.Max(_viewportWidth - scaledWidth, Math.Min(0, OffsetX));
            else
                OffsetX = (_viewportWidth - scaledWidth) / 2;

            // Вертикаль
            if (scaledHeight > _viewportHeight)
                OffsetY = Math.Max(_viewportHeight - scaledHeight, Math.Min(0, OffsetY));
            else
                OffsetY = (_viewportHeight - scaledHeight) / 2;
        }

        public (double appliedX, double appliedY) Pan(double deltaX, double deltaY)
        {
            double oldX = OffsetX;
            double oldY = OffsetY;

            OffsetX += deltaX;
            OffsetY += deltaY;
            RecalcOffset();

            return (OffsetX - oldX, OffsetY - oldY);
        }

        public void ZoomAtPoint(Point canvasPoint, double newZoom)
        {
            double oldZoom = Zoom;
            newZoom = Math.Max(1.0, Math.Min(3.0, newZoom));
            if (Math.Abs(newZoom - oldZoom) < 0.001) return;

            double dx = canvasPoint.X - (canvasPoint.X * (newZoom / oldZoom));
            double dy = canvasPoint.Y - (canvasPoint.Y * (newZoom / oldZoom));

            Zoom = newZoom;

            OffsetX += dx;
            OffsetY += dy;
            RecalcOffset();
        }
    }
}