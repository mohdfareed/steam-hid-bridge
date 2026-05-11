using System.Collections.Generic;

namespace SteamHidBridge.App.Configuration;

internal enum AppTheme
{
    System,
    Light,
    Dark
}

internal enum BridgeInputMode
{
    LegacyMouse,
    SteamInputActions
}

internal enum BridgeOutputMode
{
    None,
    Board
}

internal sealed class GameProfile
{
    public string Title { get; set; } = string.Empty;
    public string Executable { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public BridgeInputMode InputMode { get; set; } = BridgeInputMode.LegacyMouse;
    public BridgeOutputMode OutputMode { get; set; } = BridgeOutputMode.Board;
    public List<string> ReceiverProcesses { get; set; } = [];
}

internal sealed class GeneralSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public int? BoardPort { get; set; }
    public string SrmManifestPath { get; set; } = "";
}

internal sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public Dictionary<string, GameProfile> Games { get; set; } = [];
}
