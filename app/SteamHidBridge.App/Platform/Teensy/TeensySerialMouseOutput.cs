using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using SteamHidBridge.App.Core;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Platform.Teensy;

internal sealed class TeensySerialMouseOutput(int? port) : IMouseOutputTarget
{
    private const int BaudRate = 115200;
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(1);
    private readonly Lock syncLock = new();
    private readonly byte[] payload = new byte[HidInputReport.WireSize];
    private readonly byte[] frameBuffer = new byte[BridgeFrame.HeaderSize + HidInputReport.WireSize + BridgeFrame.ChecksumSize];
    private SerialPort? serialPort;
    private byte sequence;
    private long nextConnectAttempt;
    private int? configuredPortNumber = port;
    private bool isDisposed;
    private OutputStatus status = new(OutputConnectionState.Disconnected);

    public OutputStatus GetStatus()
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

    public void WriteFrame(MouseInputFrame frame)
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
                status = new(OutputConnectionState.Connected, port.PortName, LastFrame: frame);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
            {
                Trace.TraceError($"board-write-failed{Environment.NewLine}{ex}");
                status = new(OutputConnectionState.Error, port.PortName, OutputError.WriteFailed, frame);
                ClosePort();
            }
        }
    }

    public void ResetState()
    {
        WriteFrame(new MouseInputFrame(0, 0, 0, MouseButtons.None));
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
            ? new OutputStatus(OutputConnectionState.Disconnected, ToWindowsPortName(portNumber))
            : new OutputStatus(OutputConnectionState.Disconnected);
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
        string[] teensyPorts = FindLikelyTeensyPorts();
        if (teensyPorts.Length == 0)
        {
            return ports;
        }

        List<string> ordered = [.. teensyPorts];
        foreach (string port in ports)
        {
            if (!ordered.Contains(port, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(port);
            }
        }

        return [.. ordered];
    }

    private static string[] FindLikelyTeensyPorts()
    {
        const string enumPath = @"SYSTEM\CurrentControlSet\Enum\USB";
        using RegistryKey? root = Registry.LocalMachine.OpenSubKey(enumPath);
        if (root is null)
        {
            return [];
        }

        HashSet<string> ports = new(StringComparer.OrdinalIgnoreCase);
        foreach (string vendorKeyName in root.GetSubKeyNames())
        {
            if (!vendorKeyName.Contains("VID_16C0", StringComparison.OrdinalIgnoreCase)
                && !vendorKeyName.Contains("TEENSY", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using RegistryKey? vendorKey = root.OpenSubKey(vendorKeyName);
            if (vendorKey is null)
            {
                continue;
            }

            foreach (string instanceKeyName in vendorKey.GetSubKeyNames())
            {
                using RegistryKey? instanceKey = vendorKey.OpenSubKey(instanceKeyName);
                using RegistryKey? deviceParameters = instanceKey?.OpenSubKey("Device Parameters");
                if (deviceParameters?.GetValue("PortName") is string portName && !string.IsNullOrWhiteSpace(portName))
                {
                    _ = ports.Add(portName.Trim());
                }
            }
        }

        string[] resolved = [.. ports];
        Array.Sort(resolved, StringComparer.OrdinalIgnoreCase);
        return resolved;
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
