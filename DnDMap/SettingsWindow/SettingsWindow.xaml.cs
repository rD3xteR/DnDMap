using DnDMap.SettingsWindow.ViewModel;
using System.Windows;

namespace DnDMap.SettingsWindow
{
    /// <summary>
    /// Логика взаимодействия для SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, EventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;
            vm.OnClosing();
        }
    }
}
