using System;
using System.Threading;
using System.Threading.Tasks;
using Viiper.Client;

namespace SteamHidBridge.App.Platform.Viiper;

internal static class ViiperProbe
{
    public static async Task CheckAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        ViiperClient client = new(host, port);
        _ = await client.BusListAsync(timeout.Token).ConfigureAwait(false);
    }
}
