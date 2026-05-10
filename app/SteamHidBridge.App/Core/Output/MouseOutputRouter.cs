using System;
using System.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core.Input;

namespace SteamHidBridge.App.Core.Output;

public sealed class MouseOutputRouter(BridgeOutputMode outputMode, string teensyPort) : IMouseInputConsumer, IOutputStatusProvider, IDisposable
{
    private readonly Lock syncLock = new();
    private readonly TeensySerialMouseOutput teensyOutput = new(teensyPort);
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
                    BridgeOutputMode.VisualizeOnly => "Visualize only",
                    BridgeOutputMode.Teensy => teensyOutput.StatusText,
                    BridgeOutputMode.VirtualMouseDriver => driverOutput.StatusText,
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

    public void SetTeensyPort(string value)
    {
        teensyOutput.SetPort(value);
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
                case BridgeOutputMode.Teensy:
                    teensyOutput.Refresh();
                    break;
                case BridgeOutputMode.VirtualMouseDriver:
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
                case BridgeOutputMode.Teensy:
                    teensyOutput.Consume(frame);
                    break;
                case BridgeOutputMode.VirtualMouseDriver:
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
            teensyOutput.Dispose();
            driverOutput.Dispose();
        }
    }
}
