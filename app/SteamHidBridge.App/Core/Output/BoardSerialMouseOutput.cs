using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using SteamHidBridge.App.Configuration;
using SteamHidBridge.App.Core.Input;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Core.Output;

internal sealed class BoardSerialMouseOutput(int? configuredPortNumber) : IMouseInputConsumer, IOutputStatusProvider, IDisposable
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

    public OutputStatus Status
    {
        get
        {
            lock (syncLock)
            {
                return field;
            }
        }

        private set;
    } = new(BridgeOutputMode.Board, OutputConnectionState.Disconnected);

    public void SetPort(int? value)
    {
        lock (syncLock)
        {
            configuredPortNumber = value;
            ClosePort();
            nextConnectAttempt = 0;
            Status = new(BridgeOutputMode.Board, OutputConnectionState.Disconnected);
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
                Status = new(BridgeOutputMode.Board, OutputConnectionState.Error, Error: OutputError.FrameEncodeFailed);
                return;
            }

            try
            {
                port.Write(frameBuffer, 0, bytesWritten);
                Status = new(BridgeOutputMode.Board, OutputConnectionState.Connected, port.PortName);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
            {
                Trace.TraceError($"board-write-failed{Environment.NewLine}{ex}");
                Status = new(BridgeOutputMode.Board, OutputConnectionState.Error, port.PortName, OutputError.WriteFailed);
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
                Status = new(BridgeOutputMode.Board, OutputConnectionState.Connected, portName);
                return serialPort;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
            {
                port.Dispose();
            }
        }

        Status = configuredPortNumber is int portNumber
            ? new OutputStatus(BridgeOutputMode.Board, OutputConnectionState.Disconnected, ToWindowsPortName(portNumber))
            : new OutputStatus(BridgeOutputMode.Board, OutputConnectionState.Disconnected);
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
