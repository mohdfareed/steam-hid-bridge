using System.Collections.Generic;

namespace SteamHidBridge.App.Input;

public sealed class MouseInputPipeline(IEnumerable<IMouseInputConsumer> previewConsumers, IEnumerable<IMouseInputConsumer> forwardingConsumers)
{
    public void Publish(MouseInputFrame frame, bool forwardingEnabled)
    {
        foreach (IMouseInputConsumer consumer in previewConsumers)
        {
            consumer.Consume(frame);
        }

        if (!forwardingEnabled)
        {
            return;
        }

        foreach (IMouseInputConsumer consumer in forwardingConsumers)
        {
            consumer.Consume(frame);
        }
    }
}
