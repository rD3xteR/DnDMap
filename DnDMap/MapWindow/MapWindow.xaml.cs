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

        // Сенсорный ввод (мультитач)
        private bool _isTouchDraggingToken;
        private Point _lastTouchDragPoint;

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
                vm.MoveSelectedToken(e.GetPosition(MainCanvas));
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

        // ===================== Сенсорный ввод (мультитач) =====================
        // Один палец  — панорама карты либо перетаскивание токена (по точке старта).
        // Два пальца  — pinch-зум к центру жеста (+ панорама за движением центра).

        // Подавляем промоцию тач-событий в события мыши, иначе жест
        // обрабатывался бы дважды (манипуляция + панорама мышью).
        private void MainCanvas_StylusDown(object sender, System.Windows.Input.StylusDownEventArgs e)
        {
            e.Handled = true;
        }

        private void MainCanvas_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            // Координаты жеста — в пространстве MainCanvas (пространстве карты),
            // с учётом его RenderTransform, как и у GetPosition(MainCanvas).
            e.ManipulationContainer = MainCanvas;
        }

        private void MainCanvas_ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;

            if (IsHitOnToken(e.ManipulationOrigin, out var token))
            {
                _isTouchDraggingToken = true;
                vm.SetSelectedToken(e.ManipulationOrigin, token!);
                _lastTouchDragPoint = e.ManipulationOrigin;
            }
        }

        private void MainCanvas_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;
            if (e.IsInertial) return; // инерцию после подъёма пальцев не используем

            var translation = e.DeltaManipulation.Translation;

            if (_isTouchDraggingToken)
            {
                // Токен двигается вслед за пальцем (координаты уже в пространстве карты)
                _lastTouchDragPoint = new Point(
                    _lastTouchDragPoint.X + translation.X,
                    _lastTouchDragPoint.Y + translation.Y);
                vm.MoveSelectedToken(_lastTouchDragPoint);
                e.Handled = true;
                return;
            }

            // Панорама: Translation приходит в единицах контента,
            // а OffsetX/Y — в экранных, поэтому умножаем на текущий зум.
            vm.Pan(translation.X * vm.Zoom, translation.Y * vm.Zoom);

            // Pinch-зум: масштаб жеста к текущему центру между пальцами
            double scale = (e.DeltaManipulation.Scale.X + e.DeltaManipulation.Scale.Y) / 2;
            if (Math.Abs(scale - 1.0) > 0.001)
            {
                var origin = e.ManipulationOrigin;
                var cumulative = e.CumulativeManipulation.Translation;
                Point gestureCenter = new Point(origin.X + cumulative.X, origin.Y + cumulative.Y);
                vm.ZoomAtPoint(gestureCenter, vm.Zoom * scale);
            }

            e.Handled = true;
        }

        private void MainCanvas_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            _isTouchDraggingToken = false;
        }

        // Подавляем системный "отскок" контента при достижении границ жеста
        private void MainCanvas_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
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