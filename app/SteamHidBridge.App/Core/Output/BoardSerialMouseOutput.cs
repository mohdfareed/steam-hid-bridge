using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core.Input;
using SteamHidBridge.App.Platform.App;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Core.Output;

public sealed class BoardSerialMouseOutput(string configuredPort) : IMouseInputConsumer, IOutputStatusProvider, IDisposable
{
    private const int BaudRate = 115200;
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(1);
    private readonly Lock syncLock = new();
    private readonly byte[] payload = new byte[HidInputReport.WireSize];
    private readonly byte[] frameBuffer = new byte[BridgeFrame.HeaderSize + HidInputReport.WireSize + BridgeFrame.ChecksumSize];
    private SerialPort? serialPort;
    private byte sequence;
    private long nextConnectAttempt;
    private string configuredPort = SerialPortSelection.ToWindowsPortName(configuredPort);
    private string statusText = "Board disconnected";
    private bool isDisposed;

    public string StatusText
    {
        get
        {
            lock (syncLock)
            {
                return statusText;
            }
        }
    }

    public void SetPort(string value)
    {
        lock (syncLock)
        {
            configuredPort = SerialPortSelection.ToWindowsPortName(value);
            ClosePort();
            nextConnectAttempt = 0;
            statusText = "Board disconnected";
        }
    }

    public void Refresh()
    {
        lock (syncLock)
        {
            if (!isDisposed)
            {
                _ = GetOrConnectPort();
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

            SerialPort? port = GetOrConnectPort();
            if (port is null)
            {
                return;
            }

            HidInputReport report = new(
                frame.PointerDeltaX,
                frame.PointerDeltaY,
                frame.VerticalWheel,
                frame.Buttons,
                KeyboardModifiers.None,
                KeyboardUsageId: 0);

            report.WriteTo(payload);
            BridgeFrame bridgeFrame = new(BridgeCommand.HidInput, sequence++, payload);
            if (!bridgeFrame.TryWrite(frameBuffer, out int bytesWritten))
            {
                statusText = "Board frame encode failed";
                return;
            }

            try
            {
                port.Write(frameBuffer, 0, bytesWritten);
                statusText = $"Board connected: {port.PortName}";
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
            {
                AppLog.WriteException("board-write-failed", ex);
                statusText = $"Board write failed: {port.PortName}";
                ClosePort();
            }
        }
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            isDisposed = true;
            ClosePort();
        }
    }

    private SerialPort? GetOrConnectPort()
    {
        if (serialPort?.IsOpen == true)
        {
            return serialPort;
        }

        long now = Environment.TickCount64;
        if (now < nextConnectAttempt)
        {
            return null;
        }

        nextConnectAttempt = now + (long)ReconnectInterval.TotalMilliseconds;
        foreach (string portName in CandidatePorts())
        {
            SerialPort port = new(portName, BaudRate)
            {
                ReadTimeout = 5,
                WriteTimeout = 5,
                DtrEnable = true,
                RtsEnable = true
            };

            try
            {
                port.Open();
                serialPort = port;
                statusText = $"Board connected: {portName}";
                AppLog.Write($"board connected port={portName}");
                return serialPort;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
            {
                AppLog.Write($"board connect failed port={portName} error={ex.Message}");
                port.Dispose();
            }
        }

        statusText = configuredPort.Equals(SerialPortSelection.Auto, StringComparison.OrdinalIgnoreCase)
            ? "Board disconnected: no serial port opened"
            : $"Board disconnected: {configuredPort}";
        return null;
    }

    private string[] CandidatePorts()
    {
        if (!configuredPort.Equals(SerialPortSelection.Auto, StringComparison.OrdinalIgnoreCase))
        {
            return [configuredPort];
        }

        string[] ports = SerialPort.GetPortNames();
        Array.Sort(ports, StringComparer.OrdinalIgnoreCase);
        return ports;
    }

    private void ClosePort()
    {
        if (serialPort is null)
        {
            return;
        }

        try
        {
            serialPort.Dispose();
        }
        finally
        {
            serialPort = null;
        }
    }

}
