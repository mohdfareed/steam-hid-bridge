using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Transport;

public interface IBridgeTransport
{
    string Name { get; }
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<BridgeTransportResult> SendAsync(BridgeFrame frame, CancellationToken cancellationToken);
}

public readonly record struct BridgeTransportResult(bool Accepted, int ByteCount, string Detail);
