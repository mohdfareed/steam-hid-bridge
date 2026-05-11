using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Core;

internal sealed class BoardSerialMouseOutput(int? configuredPortNumber) : IDisposable
{
    private const int BaudRate = 115200;
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(1);
    private readonly Lock syncLock = new();
    private readonly byte[] payload = new byte[HidInputReport.WireSize];
    private readonly byte[] frameBuffer = new byte[BridgeFrame.HeaderSize + HidInputReport.WireSize + BridgeFrame.ChecksumSize];
    private SerialPort? serialPort;
    private byte sequence;
    private long nextConnectAttempt;
    private int? configuredPortNumber = configuredPortNumber;
    private bool isDisposed;
    private BoardOutputStatus status = new(OutputConnectionState.Disconnected);

    public BoardOutputStatus GetStatus()
    {
        lock (syncLock)
        {
            return status;
        }
    }

    public void SetPort(int? value)
    {
        lock (syncLock)
        {
            configuredPortNumber = value;
            ClosePort();
            nextConnectAttempt = 0;
            status = new(OutputConnectionState.Disconnected);
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
                frame.Buttons);

            report.WriteTo(payload);
            BridgeFrame bridgeFrame = new(BridgeCommand.HidInput, sequence++, payload);
            if (!bridgeFrame.TryWrite(frameBuffer, out int bytesWritten))
            {
                status = new(OutputConnectionState.Error, Error: OutputError.FrameEncodeFailed);
                return;
            }

            try
            {
                port.Write(frameBuffer, 0, bytesWritten);
                status = new(OutputConnectionState.Connected, port.PortName);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
            {
                Trace.TraceError($"board-write-failed{Environment.NewLine}{ex}");
                status = new(OutputConnectionState.Error, port.PortName, OutputError.WriteFailed);
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
                status = new(OutputConnectionState.Connected, portName);
                return serialPort;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
            {
                port.Dispose();
            }
        }

        status = configuredPortNumber is int portNumber
            ? new BoardOutputStatus(OutputConnectionState.Disconnected, ToWindowsPortName(portNumber))
            : new BoardOutputStatus(OutputConnectionState.Disconnected);
        return null;
    }

    private string[] CandidatePorts()
    {
        if (configuredPortNumber is int portNumber)
        {
            return [ToWindowsPortName(portNumber)];
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

    private static string ToWindowsPortName(int portNumber)
    {
        return $"COM{portNumber}";
    }
}
