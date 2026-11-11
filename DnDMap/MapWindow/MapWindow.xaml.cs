using DnDMap.MapWindow.ViewModel;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace DnDMap.MapWindow
{
    public partial class MapWindow : Window
    {
        private bool _isPanning;
        private Point _lastPanPoint;
        private bool _isDraggingToken;

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

        // Нажатие левой кнопки
        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;

            // Canvas без трансформаций: координаты уже экранные (пиксели окна).
            var screenPoint = e.GetPosition(MainCanvas);
            var token = vm.HitTest(screenPoint);

            if (token is not null)
            {
                // Начинаем перетаскивание токена
                _isDraggingToken = true;
                vm.BeginTokenDrag(token, screenPoint);
                MainCanvas.CaptureMouse();
                e.Handled = true;
            }
            else
            {
                // Начинаем панорамирование
                _isPanning = true;
                _lastPanPoint = screenPoint;
                MainCanvas.CaptureMouse();
                e.Handled = true;
            }
        }

        // Движение мыши
        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;

            var screenPoint = e.GetPosition(MainCanvas);

            if (_isDraggingToken)
            {
                vm.DragToken(screenPoint);
                e.Handled = true;
            }
            else if (_isPanning)
            {
                vm.PanByScreen(screenPoint.X - _lastPanPoint.X, screenPoint.Y - _lastPanPoint.Y);
                _lastPanPoint = screenPoint;
                e.Handled = true;
            }
        }

        // Отпускание левой кнопки
        private void MainCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;

            if (_isDraggingToken)
            {
                _isDraggingToken = false;
                vm.EndTokenDrag();
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
                vm.ZoomAt(e.GetPosition(MainCanvas), vm.Zoom + delta);
                e.Handled = true;
            }
        }

        // Зум слайдером
        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DataContext is not MapViewModel vm) return;
            if (_isPanning || _isDraggingToken) return;
            if (Math.Abs(e.NewValue - vm.Zoom) < 0.001) return;

            vm.ZoomTo(e.NewValue);
        }

        // Сброс зума и позиции
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;
            vm.ResetView();
        }

        // Удаление токена по Delete
        private void MainCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;
            if (e.Key != Key.Delete) return;
            if (vm.SelectedTokenId is null) return;

            vm.RemoveSelectedToken();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;
            vm.OnClosing();
        }
    }
}
