using System.Collections.Generic;

namespace SteamHidBridge.App.Configuration;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public enum BridgeInputMode
{
    LegacyMouse,
    SteamInputActions
}

public enum BridgeOutputMode
{
    None,
    Board
}

public sealed class GameProfile
{
    public string Title { get; set; } = string.Empty;
    public string Executable { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public BridgeInputMode InputMode { get; set; } = BridgeInputMode.LegacyMouse;
    public BridgeOutputMode OutputMode { get; set; } = BridgeOutputMode.Board;
    public List<string> ReceiverProcesses { get; set; } = [];
}

public sealed class GeneralSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string BoardPort { get; set; } = "auto";
    public string SrmManifestPath { get; set; } = "";
}

public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public Dictionary<string, GameProfile> Games { get; set; } = [];
}
