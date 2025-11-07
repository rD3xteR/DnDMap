using DnDMap.Core.BaseClasses;
using DnDMap.Core.Enums;

namespace DnDMap.Models
{
    public class EntitySizeItem : ViewModelBase
    {
        public EntitySize Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        private EntitySize _size;
        private string _displayName;
    }
}
