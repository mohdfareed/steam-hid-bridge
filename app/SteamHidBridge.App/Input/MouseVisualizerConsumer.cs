using System;

namespace SteamHidBridge.App.Input;

public sealed class MouseVisualizerConsumer(Action<MouseInputFrame> onFrame) : IMouseInputConsumer
{
    public void Consume(MouseInputFrame frame)
    {
        onFrame(frame);
    }
}
