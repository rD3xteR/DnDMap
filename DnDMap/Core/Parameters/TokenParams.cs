using DnDMap.Core.Enums;

namespace DnDMap.Core.Parameters
{
    public class TokenParams(EntitySize entitySize, string imgPath)
    {
        public EntitySize EntitySize { get; } = entitySize;
        public string TokenImagePath { get; } = imgPath;
    }
}
