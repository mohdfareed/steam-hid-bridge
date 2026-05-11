using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SteamHidBridge.App.Core;
using SteamHidBridge.Protocol;
using Viiper.Client;
using Viiper.Client.Devices.Mouse;
using Viiper.Client.Types;

namespace SteamHidBridge.App.Platform.Viiper;

internal sealed class ViiperMouseOutput : IDisposable
{
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(1);
    private readonly Lock syncLock = new();
    private readonly Channel<QueuedFrame> frameQueue = Channel.CreateUnbounded<QueuedFrame>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task writerTask;
    private ViiperClient? client;
    private ViiperDevice? device;
    private string host;
    private int port;
    private long nextConnectAttempt;
    private long generation;
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
            generation++;
            if (value)
            {
                nextConnectAttempt = 0;
                status = new(OutputConnectionState.Disconnected, EndpointText(host, port));
                return;
            }

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
            generation++;
            nextConnectAttempt = 0;
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

    public void Consume(MouseInputFrame frame)
    {
        long currentGeneration;
        lock (syncLock)
        {
            if (isDisposed || !enabled)
            {
                return;
            }

            currentGeneration = generation;
        }

        _ = frameQueue.Writer.TryWrite(new QueuedFrame(currentGeneration, frame));
    }

    public void ResetState()
    {
        string endpoint;
        MouseInputFrame zeroFrame = new(0, 0, 0, MouseButtons.None);
        ViiperDevice? activeDevice;
        long currentGeneration;

        lock (syncLock)
        {
            if (isDisposed)
            {
                return;
            }

            generation++;
            currentGeneration = generation;
            endpoint = EndpointText(host, port);
            activeDevice = enabled ? device : null;
        }

        if (activeDevice is not null)
        {
            try
            {
                activeDevice.SendAsync(ToMouseInput(zeroFrame)).GetAwaiter().GetResult();
                lock (syncLock)
                {
                    if (!isDisposed && generation == currentGeneration)
                    {
                        status = new OutputStatus(OutputConnectionState.Connected, endpoint, LastFrame: zeroFrame);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"viiper-reset-failed{Environment.NewLine}{ex}");
                lock (syncLock)
                {
                    if (!isDisposed && generation == currentGeneration)
                    {
                        status = new OutputStatus(OutputConnectionState.Error, endpoint, OutputError.WriteFailed, zeroFrame);
                        DisconnectLocked();
                    }
                }
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
            generation++;
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

        cancellation.Dispose();
    }

    private async Task RunWriterAsync()
    {
        QueuedFrame? carry = null;
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                QueuedFrame queued = carry ?? await frameQueue.Reader.ReadAsync(cancellation.Token).ConfigureAwait(false);
                carry = null;

                MouseInputFrame mergedFrame = queued.Frame;
                while (frameQueue.Reader.TryRead(out QueuedFrame next))
                {
                    if (next.Generation == queued.Generation && next.Frame.Buttons == mergedFrame.Buttons)
                    {
                        mergedFrame = Merge(mergedFrame, next.Frame);
                        continue;
                    }

                    carry = next;
                    break;
                }

                await SendFrameAsync(queued.Generation, mergedFrame).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SendFrameAsync(long queuedGeneration, MouseInputFrame frame)
    {
        ViiperDevice? activeDevice;
        string endpoint;

        lock (syncLock)
        {
            endpoint = EndpointText(host, port);
            if (isDisposed || !enabled || queuedGeneration != generation || device is null)
            {
                return;
            }

            activeDevice = device;
        }

        try
        {
            await activeDevice.SendAsync(ToMouseInput(frame)).ConfigureAwait(false);
            if (frame.PointerDeltaX != 0 || frame.PointerDeltaY != 0 || frame.VerticalWheel != 0)
            {
                MouseInputFrame settledFrame = new(0, 0, 0, frame.Buttons);
                await activeDevice.SendAsync(ToMouseInput(settledFrame)).ConfigureAwait(false);
            }

            lock (syncLock)
            {
                if (!isDisposed && enabled && queuedGeneration == generation)
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
                if (!isDisposed && queuedGeneration == generation)
                {
                    status = new OutputStatus(OutputConnectionState.Error, endpoint, OutputError.WriteFailed, frame);
                    DisconnectLocked();
                }
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
                    status = new OutputStatus(OutputConnectionState.Disconnected, EndpointText(host, port), LastFrame: status.LastFrame);
                }
            };
            status = new OutputStatus(OutputConnectionState.Connected, endpoint, LastFrame: status.LastFrame);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"viiper-connect-failed{Environment.NewLine}{ex}");
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

    private static MouseInputFrame Merge(MouseInputFrame current, MouseInputFrame next)
    {
        return new MouseInputFrame(
            SaturatingAdd(current.PointerDeltaX, next.PointerDeltaX),
            SaturatingAdd(current.PointerDeltaY, next.PointerDeltaY),
            SaturatingAdd(current.VerticalWheel, next.VerticalWheel),
            current.Buttons);
    }

    private static short SaturatingAdd(short left, short right)
    {
        int value = left + right;
        return value switch
        {
            > short.MaxValue => short.MaxValue,
            < short.MinValue => short.MinValue,
            _ => (short)value
        };
    }

    private static sbyte SaturatingAdd(sbyte left, sbyte right)
    {
        int value = left + right;
        return value switch
        {
            > sbyte.MaxValue => sbyte.MaxValue,
            < sbyte.MinValue => sbyte.MinValue,
            _ => (sbyte)value
        };
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

    private readonly record struct QueuedFrame(long Generation, MouseInputFrame Frame);
}
