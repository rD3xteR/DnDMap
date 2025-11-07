namespace DnDMap.Core;

public static class GlobalSettings
{
    public static event Action? TokenDiameterChanged;

    public static double TokenDiameter
    {
        get => _tokenDiameter;
        set
        {
            if (_tokenDiameter.Equals(value)) return;

            _tokenDiameter = value;
            TokenDiameterChanged?.Invoke();
        }
    }

    public static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg" };

    private static double _tokenDiameter = 100;
}