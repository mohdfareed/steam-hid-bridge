namespace SteamHidBridge.App.Transport;

public readonly record struct BridgeTransportResult(bool Accepted, int ByteCount, string Detail);
