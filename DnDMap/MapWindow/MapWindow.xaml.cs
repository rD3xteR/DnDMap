using DnDMap.MapWindow.ViewModel;
using System.Windows;

namespace DnDMap.MapWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MapWindow : Window
    {
        public MapWindow()
        {
            InitializeComponent();
        }

        private void MainCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;
            vm.MouseDownCommand.Execute(e.GetPosition(MainCanvas));
        }

        private void MainCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;
            if (e.LeftButton is System.Windows.Input.MouseButtonState.Released) return;

            vm.MouseMoveCommand.Execute(e.GetPosition(MainCanvas));
        }

        private void MainCanvas_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext is not MapViewModel vm) return;
            if (e.Key is not System.Windows.Input.Key.Delete) return;
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