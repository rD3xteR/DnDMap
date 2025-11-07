using DnDMap.Core.BaseClasses;
using DnDMap.Core.Command;

namespace DnDMap.SettingsWindow.ViewModel
{
    public partial class SettingsViewModel : ViewModelBase
    {
        public SettingsViewModel()
        {
            #region Commands

            BrowseMapsCommand = new RelayCommand(BrowseMapsAsync);
            BrowseTokensCommand = new RelayCommand(BrowseTokensAsync);
            AddTokenCommand = new RelayCommand(AddToken);
            OpenMapCommand = new RelayCommand(OpenMap);

            #endregion
        }
    }
}
