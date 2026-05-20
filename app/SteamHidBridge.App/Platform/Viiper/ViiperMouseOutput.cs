using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SteamHidBridge.App.Core;
using SteamHidBridge.Protocol;
using Viiper.Client;
using Viiper.Client.Devices.Mouse;
using Viiper.Client.Types;

namespace SteamHidBridge.App.Platform.Viiper;

internal sealed class ViiperMouseOutput : IMouseOutputTarget
{
    private static readonly TimeSpan DrainInterval = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(1);
    private readonly Lock syncLock = new();
    private readonly SemaphoreSlim wakeSignal = new(0, 1);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task writerTask;
    private readonly Queue<MouseInputFrame> pendingReports = [];
    private ViiperClient? client;
    private ViiperDevice? device;
    private string host;
    private int port;
    private int pendingSignal;
    private int accumulatedDeltaX;
    private int accumulatedDeltaY;
    private int accumulatedWheel;
    private long nextConnectAttempt;
    private MouseButtons currentButtons;
    private bool enabled;
    private bool isDisposed;
    private OutputStatus status;

    public ViiperMouseOutput(string host, int port)
    {
        this.host = host;
        this.port = port;
        status = new(OutputConnectionState.Idle, EndpointText(host, port));
        writerTask = Task.Run(RunWriterAsync);
    }

    public OutputStatus GetStatus()
    {
        lock (syncLock)
        {
            return status;
        }
    }

    public void SetEnabled(bool value)
    {
        lock (syncLock)
        {
            if (isDisposed || enabled == value)
            {
                return;
            }

            enabled = value;
            if (value)
            {
                nextConnectAttempt = 0;
                status = new(OutputConnectionState.Disconnected, EndpointText(host, port));
                return;
            }

            ClearPendingLocked();
            DisconnectLocked();
            status = new(OutputConnectionState.Idle, EndpointText(host, port), LastFrame: status.LastFrame);
        }
    }

    public void SetEndpoint(string host, int port)
    {
        lock (syncLock)
        {
            this.host = host;
            this.port = port;
            nextConnectAttempt = 0;
            ClearPendingLocked();
            DisconnectLocked();
            status = enabled
                ? new OutputStatus(OutputConnectionState.Disconnected, EndpointText(host, port), LastFrame: status.LastFrame)
                : new OutputStatus(OutputConnectionState.Idle, EndpointText(host, port), LastFrame: status.LastFrame);
        }
    }

    public void Refresh()
    {
        lock (syncLock)
        {
            if (isDisposed || !enabled || device is not null)
            {
                return;
            }

            long now = Environment.TickCount64;
            if (now < nextConnectAttempt)
            {
                return;
            }

            nextConnectAttempt = now + (long)ReconnectInterval.TotalMilliseconds;
            ConnectLocked();
        }
    }

    public void WriteFrame(MouseInputFrame frame)
    {
        lock (syncLock)
        {
            if (isDisposed || !enabled || device is null)
            {
                return;
            }

            if (frame.Buttons != currentButtons)
            {
                EnqueueAccumulatedLocked();
                pendingReports.Enqueue(frame);
                currentButtons = frame.Buttons;
            }
            else
            {
                accumulatedDeltaX += frame.PointerDeltaX;
                accumulatedDeltaY += frame.PointerDeltaY;
                accumulatedWheel += frame.VerticalWheel;
            }
        }

        SignalWriter();
    }

    public void ResetState()
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            ClearPendingLocked();
            if (!enabled || device is null)
            {
                return;
            }

            MouseInputFrame zeroFrame = new(0, 0, 0, MouseButtons.None);
            try
            {
                device.SendAsync(ToMouseInput(zeroFrame)).GetAwaiter().GetResult();
                currentButtons = MouseButtons.None;
                status = new OutputStatus(OutputConnectionState.Connected, EndpointText(host, port), LastFrame: zeroFrame);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"viiper-reset-failed{Environment.NewLine}{ex}");
                status = new OutputStatus(OutputConnectionState.Error, EndpointText(host, port), OutputError.WriteFailed, zeroFrame);
                DisconnectLocked();
            }
        }
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            ClearPendingLocked();
            DisconnectLocked();
        }

        cancellation.Cancel();
        try
        {
            writerTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        wakeSignal.Dispose();
        cancellation.Dispose();
    }

    private async Task RunWriterAsync()
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                _ = await wakeSignal.WaitAsync(DrainInterval, cancellation.Token).ConfigureAwait(false);
                _ = Interlocked.Exchange(ref pendingSignal, 0);
                await FlushPendingAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task FlushPendingAsync()
    {
        List<MouseInputFrame> reports = [];
        ViiperDevice? activeDevice;
        string endpoint;

        lock (syncLock)
        {
            if (isDisposed || !enabled || device is null)
            {
                return;
            }

            activeDevice = device;
            endpoint = EndpointText(host, port);

            while (pendingReports.Count > 0)
            {
                reports.Add(pendingReports.Dequeue());
            }

            MouseInputFrame? accumulated = TakeAccumulatedLocked();
            if (accumulated is MouseInputFrame frame)
            {
                reports.Add(frame);
            }
        }

        foreach (MouseInputFrame frame in reports)
        {
            try
            {
                await activeDevice.SendAsync(ToMouseInput(frame)).ConfigureAwait(false);
                lock (syncLock)
                {
                    if (!isDisposed && enabled && device is not null)
                    {
                        status = new OutputStatus(OutputConnectionState.Connected, endpoint, LastFrame: frame);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"viiper-write-failed{Environment.NewLine}{ex}");
                lock (syncLock)
                {
                    if (!isDisposed)
                    {
                        status = new OutputStatus(OutputConnectionState.Error, endpoint, OutputError.WriteFailed, frame);
                        ClearPendingLocked();
                        DisconnectLocked();
                    }
                }

                return;
            }
        }
    }

    private void ConnectLocked()
    {
        string endpoint = EndpointText(host, port);
        try
        {
            client ??= new ViiperClient(host, port);
            uint[] buses = client.BusListAsync(CancellationToken.None).GetAwaiter().GetResult().Buses;
            uint busId = buses.Length > 0
                ? buses[0]
                : client.BusCreateAsync(null, CancellationToken.None).GetAwaiter().GetResult().BusID;

            Device deviceInfo = client.BusDeviceAddAsync(
                busId,
                new DeviceCreateRequest
                {
                    Type = "mouse"
                },
                CancellationToken.None).GetAwaiter().GetResult();
            device = client.ConnectDeviceAsync(busId, deviceInfo.DevId, CancellationToken.None).GetAwaiter().GetResult();
            device.OnDisconnect = () =>
            {
                lock (syncLock)
                {
                    if (isDisposed)
                    {
                        return;
                    }

                    device = null;
                    ClearPendingLocked();
                    status = new OutputStatus(OutputConnectionState.Disconnected, EndpointText(host, port), LastFrame: status.LastFrame);
                }
            };
            status = new OutputStatus(OutputConnectionState.Connected, endpoint, LastFrame: status.LastFrame);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"viiper-connect-failed{Environment.NewLine}{ex}");
            ClearPendingLocked();
            DisconnectLocked();
            status = new OutputStatus(OutputConnectionState.Error, endpoint, OutputError.ConnectFailed, status.LastFrame);
        }
    }

    private void DisconnectLocked()
    {
        try
        {
            device?.Dispose();
        }
        catch
        {
        }
        finally
        {
            device = null;
        }

        try
        {
            client?.Dispose();
        }
        catch
        {
        }
        finally
        {
            client = null;
        }
    }

    private void SignalWriter()
    {
        if (Interlocked.Exchange(ref pendingSignal, 1) == 0)
        {
            _ = wakeSignal.Release();
        }
    }

    private MouseInputFrame? TakeAccumulatedLocked()
    {
        if (accumulatedDeltaX == 0 && accumulatedDeltaY == 0 && accumulatedWheel == 0)
        {
            return null;
        }

        MouseInputFrame frame = new(
            ClampToInt16(accumulatedDeltaX),
            ClampToInt16(accumulatedDeltaY),
            ClampToSByte(accumulatedWheel),
            currentButtons);
        accumulatedDeltaX = 0;
        accumulatedDeltaY = 0;
        accumulatedWheel = 0;
        return frame;
    }

    private void EnqueueAccumulatedLocked()
    {
        MouseInputFrame? accumulated = TakeAccumulatedLocked();
        if (accumulated is MouseInputFrame frame)
        {
            pendingReports.Enqueue(frame);
        }
    }

    private void ClearPendingLocked()
    {
        pendingReports.Clear();
        accumulatedDeltaX = 0;
        accumulatedDeltaY = 0;
        accumulatedWheel = 0;
        currentButtons = MouseButtons.None;
        _ = Interlocked.Exchange(ref pendingSignal, 0);
        while (wakeSignal.CurrentCount > 0)
        {
            _ = wakeSignal.Wait(0);
        }
    }

    private static MouseInput ToMouseInput(MouseInputFrame frame)
    {
        return new MouseInput
        {
            Buttons = (byte)frame.Buttons,
            Dx = frame.PointerDeltaX,
            Dy = frame.PointerDeltaY,
            Wheel = frame.VerticalWheel,
            Pan = 0
        };
    }

    private static string EndpointText(string host, int port)
    {
        return $"{host}:{port}";
    }

    private static short ClampToInt16(int value)
    {
        return value > short.MaxValue ? short.MaxValue : value < short.MinValue ? short.MinValue : (short)value;
    }

    private static sbyte ClampToSByte(int value)
    {
        return value > sbyte.MaxValue ? sbyte.MaxValue : value < sbyte.MinValue ? sbyte.MinValue : (sbyte)value;
    }
}
