using System.Windows.Markup;

namespace DnDMap.Core.Extensions
{
    public class EnumBindingSourceExtension : MarkupExtension
    {
        public Type EnumType { get; set; }

        public EnumBindingSourceExtension(Type enumType)
        {
            if (enumType == null || !enumType.IsEnum)
                throw new ArgumentNullException(nameof(enumType));

            EnumType = enumType;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Enum.GetValues(EnumType);
        }
    }
}
