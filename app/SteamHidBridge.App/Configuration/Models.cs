using System.Collections.Generic;
using System.Linq;
using SteamHidBridge.App.Platform.App;

namespace SteamHidBridge.App.Configuration;

internal enum AppTheme
{
    System,
    Light,
    Dark
}

internal enum BridgeOutputMode
{
    None,
    Board,
    Viiper
}

internal sealed class GameProfile
{
    public string Title { get; set; } = string.Empty;
    public string Executable { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public BridgeOutputMode OutputMode { get; set; } = BridgeOutputMode.Board;
    public List<string> ReceiverProcesses { get; set; } = [];

    public GameProfile Copy()
    {
        return new GameProfile
        {
            Title = Title,
            Executable = Executable,
            Arguments = Arguments,
            WorkingDirectory = WorkingDirectory,
            OutputMode = OutputMode,
            ReceiverProcesses = [.. ReceiverProcesses]
        };
    }

    public bool ContentEquals(GameProfile other)
    {
        return Title == other.Title
            && Executable == other.Executable
            && Arguments == other.Arguments
            && WorkingDirectory == other.WorkingDirectory
            && OutputMode == other.OutputMode
            && ReceiverProcesses.SequenceEqual(other.ReceiverProcesses);
    }
}

internal sealed class GeneralSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public int? BoardPort { get; set; }
    public string SrmManifestPath { get; set; } = AppDataPaths.SrmManifestPath;
    public string ViiperHost { get; set; } = "localhost";
    public int ViiperPort { get; set; } = 3242;
}

internal sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public Dictionary<string, GameProfile> Games { get; set; } = [];
}
