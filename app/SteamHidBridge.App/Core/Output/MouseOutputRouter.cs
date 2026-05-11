using System;
using System.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core.Input;

namespace SteamHidBridge.App.Core.Output;

internal sealed class MouseOutputRouter(BridgeOutputMode outputMode, int? boardPort) : IMouseInputConsumer, IOutputStatusProvider, IDisposable
{
    private readonly Lock syncLock = new();
    private readonly BoardSerialMouseOutput boardOutput = new(boardPort);
    private BridgeOutputMode outputMode = outputMode;
    private bool isDisposed;

    public OutputStatus Status
    {
        get
        {
            lock (syncLock)
            {
                return outputMode switch
                {
                    BridgeOutputMode.None => new OutputStatus(BridgeOutputMode.None, OutputConnectionState.Idle),
                    BridgeOutputMode.Board => boardOutput.Status,
                    _ => new OutputStatus(BridgeOutputMode.None, OutputConnectionState.Error, Error: OutputError.UnknownMode)
                };
            }
        }
    }

    public void SetMode(BridgeOutputMode value)
    {
        lock (syncLock)
        {
            outputMode = value;
        }
    }

    public void SetBoardPort(int? value)
    {
        boardOutput.SetPort(value);
    }

    public void Refresh()
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            switch (outputMode)
            {
                case BridgeOutputMode.Board:
                    boardOutput.Refresh();
                    break;
                case BridgeOutputMode.None:
                    break;
                default:
                    break;
            }
        }
    }

    public void Consume(MouseInputFrame frame)
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            switch (outputMode)
            {
                case BridgeOutputMode.Board:
                    boardOutput.Consume(frame);
                    break;
                case BridgeOutputMode.None:
                    break;
                default:
                    break;
            }
        }
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            isDisposed = true;
            boardOutput.Dispose();
        }
    }
}
