using System;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Transport;

public sealed class LoopbackBridgeTransport : IBridgeTransport
{
    public string Name => "Loopback simulator";
    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task<BridgeTransportResult> SendAsync(BridgeFrame frame, CancellationToken cancellationToken)
    {
        Span<byte> encoded = stackalloc byte[BridgeFrame.HeaderSize + BridgeFrame.MaxPayloadLength + BridgeFrame.ChecksumSize];
        if (!frame.TryWrite(encoded, out int byteCount))
        {
            return Task.FromResult(new BridgeTransportResult(false, 0, "loopback could not encode frame"));
        }

        bool accepted = BridgeFrame.TryDecode(encoded[..byteCount], out BridgeFrame decoded) &&
            decoded.Command == frame.Command &&
            decoded.Sequence == frame.Sequence;

        string detail = accepted
            ? $"{byteCount} bytes validated by loopback"
            : "loopback rejected encoded frame";

        return Task.FromResult(new BridgeTransportResult(accepted, byteCount, detail));
    }
}
