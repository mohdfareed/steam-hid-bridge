namespace SteamHidBridge.App.Runtime;

public sealed record BridgeRuntimeStatus(
    bool ForwardingEnabled,
    string ForwardingText,
    string InputLoopText,
    string SteamInputText);
