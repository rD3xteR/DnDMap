using DnDMap.Core.BaseClasses;
using DnDMap.Core.Command;

namespace DnDMap.SettingsWindow.ViewModel
{
    public partial class SettingsViewModel : ViewModelBase
    {
        public SettingsViewModel()
        {
            #region Commands

            BrowseMapsCommand = new RelayCommand(BrowseMaps);
            BrowseTokensCommand = new RelayCommand(BrowseTokens);
            AddTokenCommand = new RelayCommand(AddToken);
            OpenMapCommand = new RelayCommand(OpenMap);

            #endregion

            #region Load saved paths

            SetMaps();
            SetTokens();

            #endregion
        }
    }
}
