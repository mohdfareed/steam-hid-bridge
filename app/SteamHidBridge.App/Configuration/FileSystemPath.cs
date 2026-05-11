using System;
using System.IO;

namespace SteamHidBridge.App.Configuration;

internal static class FileSystemPath
{
    public static string Normalize(string path)
    {
        string trimmed = path.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        string expanded = Environment.ExpandEnvironmentVariables(trimmed);
        if (expanded.StartsWith(@"~\", StringComparison.Ordinal) || expanded.StartsWith("~/", StringComparison.Ordinal))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = Path.Combine(home, expanded[2..]);
        }

        try
        {
            return Path.GetFullPath(expanded);
        }
        catch (Exception) when (expanded.Length > 0)
        {
            return expanded;
        }
    }
}
