using System;
using System.Collections.Generic;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.App.Core.Input;

public sealed class MouseInputRouter(
    Action<MouseInputFrame> previewFrame,
    IEnumerable<IMouseInputConsumer> forwardingConsumers,
    Func<bool> isForwardingEnabled)
{
    private const int PreviewIntervalMilliseconds = 16;
    private long eventCount;
    private long previewCount;
    private long lastTimestamp = Environment.TickCount64;
    private long lastPreviewTimestamp;
    private long lastEventCount;
    private long lastPreviewCount;
    private MouseInputFrame pendingPreviewFrame;
    private MouseButtons lastPreviewButtons;
    private MouseInputStatistics statistics = new(0, 0, 0, 0);

    public void Publish(MouseInputFrame frame)
    {
        eventCount++;
        if (isForwardingEnabled())
        {
            foreach (IMouseInputConsumer consumer in forwardingConsumers)
            {
                consumer.Consume(frame);
            }
        }

        QueuePreview(frame);
        RefreshStatistics();
    }

    public MouseInputStatistics GetStatistics()
    {
        return statistics;
    }

    private void QueuePreview(MouseInputFrame frame)
    {
        pendingPreviewFrame = Combine(pendingPreviewFrame, frame);
        long now = Environment.TickCount64;
        bool buttonChanged = frame.Buttons != lastPreviewButtons;
        bool wheelMoved = frame.VerticalWheel != 0;
        if (!buttonChanged && !wheelMoved && now - lastPreviewTimestamp < PreviewIntervalMilliseconds)
        {
            return;
        }

        previewCount++;
        previewFrame(pendingPreviewFrame);
        lastPreviewButtons = pendingPreviewFrame.Buttons;
        pendingPreviewFrame = default;
        lastPreviewTimestamp = now;
    }

    private void RefreshStatistics()
    {
        long now = Environment.TickCount64;
        long elapsed = now - lastTimestamp;
        if (elapsed < 1000)
        {
            return;
        }

        statistics = new MouseInputStatistics(
            eventCount,
            previewCount,
            (eventCount - lastEventCount) * 1000d / elapsed,
            (previewCount - lastPreviewCount) * 1000d / elapsed);

        lastTimestamp = now;
        lastEventCount = eventCount;
        lastPreviewCount = previewCount;
    }

    private static MouseInputFrame Combine(MouseInputFrame current, MouseInputFrame next)
    {
        return new MouseInputFrame(
            ClampToInt16(current.PointerDeltaX + next.PointerDeltaX),
            ClampToInt16(current.PointerDeltaY + next.PointerDeltaY),
            ClampToSByte(current.VerticalWheel + next.VerticalWheel),
            next.Buttons);
    }

    private static short ClampToInt16(int value)
    {
        if (value > short.MaxValue)
        {
            return short.MaxValue;
        }

        if (value < short.MinValue)
        {
            return short.MinValue;
        }

        return (short)value;
    }

    private static sbyte ClampToSByte(int value)
    {
        if (value > sbyte.MaxValue)
        {
            return sbyte.MaxValue;
        }

        if (value < sbyte.MinValue)
        {
            return sbyte.MinValue;
        }

        return (sbyte)value;
    }
}
