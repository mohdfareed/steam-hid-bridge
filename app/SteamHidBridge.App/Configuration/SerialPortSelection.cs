using System.Linq;

namespace SteamHidBridge.App.Configuration;

public static class SerialPortSelection
{
    public const string Auto = "auto";

    public static string Normalize(string value)
    {
        value = value.Trim();
        return string.IsNullOrWhiteSpace(value) || !value.All(char.IsDigit)
            ? Auto
            : value;
    }

    public static string ToUiText(string value)
    {
        string normalized = Normalize(value);
        return normalized.Equals(Auto, System.StringComparison.OrdinalIgnoreCase) ? string.Empty : normalized;
    }

    public static string ToWindowsPortName(string value)
    {
        string normalized = Normalize(value);
        return normalized.Equals(Auto, System.StringComparison.OrdinalIgnoreCase) ? Auto : $"COM{normalized}";
    }
}
