using DnDMap.MapWindow.ViewModel;
using DnDMap.TokenModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace DnDMap.MapWindow
{
    public partial class MapWindow : Window
    {
        private bool _isPanning;
        private Point _lastPanPoint;
        private bool _isDraggingToken;
        private Point _lastTokenDragPoint;

        public MapWindow()
        {
            InitializeComponent();
            // Подписываемся на события изменения размера окна и загрузки
            SizeChanged += (s, e) => UpdateViewportSize();
            Loaded += (s, e) => UpdateViewportSize();
        }

        // Обновление размеров видимой области для ViewModel
        private void UpdateViewportSize()
        {
            if (DataContext is MapViewModel vm && MainCanvas.ActualWidth > 0 && MainCanvas.ActualHeight > 0)
            {
                vm.SetViewportSize(MainCanvas.ActualWidth, MainCanvas.ActualHeight);
            }
        }

        // Преобразование экранных координат Canvas в исходные координаты карты
        private Point TransformToMap(Point pointOnCanvas)
        {
            if (MainCanvas.RenderTransform is TransformGroup transform)
            {
                Matrix matrix = transform.Value;
                if (matrix.HasInverse)
                {
                    matrix.Invert();
                    return matrix.Transform(pointOnCanvas);
                }
            }
            return pointOnCanvas;
        }

        // Определение, находится ли точка на каком-либо токене
        private bool IsHitOnToken(Point mousePosition, out Token? token)
        {
            var hitResult = MainCanvas.InputHitTest(mousePosition);
            token = null;
            if (hitResult == null) return false;

            var current = hitResult as DependencyObject;
            while (current != null && current != MainCanvas)
            {
                if (current is ContentPresenter presenter && presenter.Content is Token elementToken)
                {
                    token = elementToken;
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        // Нажатие левой кнопки
        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;

            var mousePos = e.GetPosition(MainCanvas);

            if (IsHitOnToken(mousePos, out var token))
            {
                // Начинаем перетаскивание токена
                _isDraggingToken = true;
                vm.SetSelectedToken(mousePos, token!);
                _lastTokenDragPoint = TransformToMap(mousePos);
                MainCanvas.CaptureMouse();
                e.Handled = true;
            }
            else
            {
                // Начинаем панорамирование
                _isPanning = true;
                _lastPanPoint = mousePos;
                MainCanvas.CaptureMouse();
                e.Handled = true;
            }
        }

        // Движение мыши
        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;

            if (_isDraggingToken)
            {
                var current = TransformToMap(e.GetPosition(MainCanvas));
                vm.MoveSelectedToken(current);
                _lastTokenDragPoint = current;
                e.Handled = true;
            }
            else if (_isPanning)
            {
                var current = e.GetPosition(MainCanvas);
                double dx = current.X - _lastPanPoint.X;
                double dy = current.Y - _lastPanPoint.Y;
                vm.Pan(dx, dy);
                _lastPanPoint = current;
                e.Handled = true;
            }
        }

        // Отпускание левой кнопки
        private void MainCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingToken)
            {
                _isDraggingToken = false;
                MainCanvas.ReleaseMouseCapture();
                e.Handled = true;
            }
            else if (_isPanning)
            {
                _isPanning = false;
                MainCanvas.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        // Зум колесом с Ctrl
        private void MainCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (DataContext is not MapViewModel vm) return;
                double delta = e.Delta > 0 ? 0.1 : -0.1;
                double newZoom = vm.Zoom + delta;
                Point mousePos = e.GetPosition(MainCanvas);
                vm.ZoomAtPoint(mousePos, newZoom);
                e.Handled = true;
            }
        }

        // Зум слайдером
        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DataContext is not MapViewModel vm) return;
            if (!_isPanning && !_isDraggingToken)
            {
                Point center = new Point(MainCanvas.ActualWidth / 2, MainCanvas.ActualHeight / 2);
                vm.ZoomAtPoint(center, e.NewValue);
            }
        }

        // Сброс зума и позиции
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;
            vm.Zoom = 1.0;
            vm.OffsetX = 0;
            vm.OffsetY = 0;
        }

        // Удаление токена по Delete
        private void MainCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;
            if (e.Key != Key.Delete) return;
            if (vm.SelectedToken is null) return;

            vm.Tokens.Remove(vm.SelectedToken);
            vm.SelectedToken = null;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;
            vm.OnClosing();
        }
    }
}