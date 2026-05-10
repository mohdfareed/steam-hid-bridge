using System;
using System.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core.Input;

namespace SteamHidBridge.App.Core.Output;

public sealed class MouseOutputRouter(BridgeOutputMode outputMode, string boardPort) : IMouseInputConsumer, IOutputStatusProvider, IDisposable
{
    private readonly Lock syncLock = new();
    private readonly BoardSerialMouseOutput boardOutput = new(boardPort);
    private readonly VirtualMouseDriverOutput driverOutput = new();
    private BridgeOutputMode outputMode = outputMode;
    private bool isDisposed;

    public string StatusText
    {
        get
        {
            lock (syncLock)
            {
                return outputMode switch
                {
                    BridgeOutputMode.None => "Visualize only",
                    BridgeOutputMode.Board => boardOutput.StatusText,
                    BridgeOutputMode.VirtualMouse => driverOutput.StatusText,
                    _ => "Unknown output mode"
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

    public void SetBoardPort(string value)
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
                case BridgeOutputMode.VirtualMouse:
                    driverOutput.Refresh();
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
                case BridgeOutputMode.VirtualMouse:
                    driverOutput.Consume(frame);
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
            driverOutput.Dispose();
        }
    }
}
