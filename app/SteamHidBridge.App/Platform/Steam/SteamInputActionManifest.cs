using System;
using System.IO;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Platform.Steam;

internal static class SteamInputActionManifest
{
    public const string ActionSet = "BridgeMouse";
    public const string Pointer = "Pointer";
    public const string LeftClick = "LeftClick";
    public const string RightClick = "RightClick";
    public const string MiddleClick = "MiddleClick";
    public const string BackClick = "BackClick";
    public const string ForwardClick = "ForwardClick";
    public const string WheelUp = "WheelUp";
    public const string WheelDown = "WheelDown";

    public static string ManifestPath => Path.Combine(AppDataPaths.RootDirectory, "steam-input", "action_manifest.vdf");
    private static string BundledManifestPath => Path.Combine(AppContext.BaseDirectory, "Steam", "action_manifest.vdf");

    public static string Write()
    {
        if (!File.Exists(BundledManifestPath))
        {
            throw new FileNotFoundException("Bundled Steam Input action manifest was not found.", BundledManifestPath);
        }

        string manifestPath = ManifestPath;
        string? directory = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        File.Copy(BundledManifestPath, manifestPath, overwrite: true);
        return manifestPath;
    }
}
