using System;
using System.IO;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Platform.Steam;

public sealed record SteamInputActionManifestResult(string ManifestPath, string? ControllerConfigPath, string AppId);

public static class SteamInputActionManifest
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

    public static SteamInputActionManifestResult Write()
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

        string appId = ResolveAppId();
        string? controllerConfigPath = null;
        string? steamPath = FindSteamPath();
        if (!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(steamPath))
        {
            string controllerConfigDirectory = Path.Combine(steamPath, "controller_config");
            _ = Directory.CreateDirectory(controllerConfigDirectory);
            controllerConfigPath = Path.Combine(controllerConfigDirectory, $"game_actions_{appId}.vdf");
            File.Copy(BundledManifestPath, controllerConfigPath, overwrite: true);
        }

        return new SteamInputActionManifestResult(manifestPath, controllerConfigPath, appId);
    }

    public static string ResolveAppId()
    {
        string[] names = ["SteamAppId", "SteamGameId", "steam_appid"];
        foreach (string name in names)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (IsNumeric(value))
            {
                return value!.Trim();
            }
        }

        return "";
    }

    private static string? FindSteamPath()
    {
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (string.IsNullOrWhiteSpace(programFilesX86))
        {
            return null;
        }

        string defaultPath = Path.Combine(programFilesX86, "Steam");
        return Directory.Exists(defaultPath) ? defaultPath : null;
    }

    private static bool IsNumeric(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsDigit(character))
            {
                return false;
            }
        }

        return true;
    }

}
