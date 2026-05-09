using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.Protocol.Tests;

[TestClass]
public sealed class BridgeFrameTests
{
    [TestMethod]
    public void EncodeDecodeRoundTripPreservesCommandSequenceAndPayload()
    {
        byte[] payload = new byte[HidInputReport.WireSize];
        var report = new HidInputReport(
            PointerDeltaX: 42,
            PointerDeltaY: -7,
            VerticalWheel: 1,
            MouseButtons: MouseButtons.Left | MouseButtons.Right,
            KeyboardModifiers: KeyboardModifiers.None,
            KeyboardUsageId: 0);
        report.WriteTo(payload);

        var source = new BridgeFrame(BridgeCommand.HidInput, Sequence: 9, payload);

        bool decoded = BridgeFrame.TryDecode(source.Encode(), out BridgeFrame frame);

        Assert.IsTrue(decoded);
        Assert.AreEqual(BridgeCommand.HidInput, frame.Command);
        Assert.AreEqual(9, frame.Sequence);
        CollectionAssert.AreEqual(payload, frame.Payload);
    }

    [TestMethod]
    public void TryDecodeRejectsCorruptChecksum()
    {
        byte[] bytes = new BridgeFrame(BridgeCommand.Ping, Sequence: 1, []).Encode();
        bytes[^1] ^= 0x7f;

        bool decoded = BridgeFrame.TryDecode(bytes, out _);

        Assert.IsFalse(decoded);
    }

    [TestMethod]
    public void TryDecodeRejectsOversizedPayloadMarker()
    {
        byte[] bytes = new BridgeFrame(BridgeCommand.Ping, Sequence: 1, []).Encode();
        bytes[6] = BridgeFrame.MaxPayloadLength + 1;

        bool decoded = BridgeFrame.TryDecode(bytes, out _);

        Assert.IsFalse(decoded);
    }

    [TestMethod]
    public void TryWriteEncodesIntoCallerProvidedBuffer()
    {
        var source = new BridgeFrame(BridgeCommand.Ping, Sequence: 2, []);
        Span<byte> buffer = stackalloc byte[BridgeFrame.HeaderSize + BridgeFrame.ChecksumSize];

        bool encoded = source.TryWrite(buffer, out int bytesWritten);

        Assert.IsTrue(encoded);
        Assert.AreEqual(buffer.Length, bytesWritten);
        Assert.IsTrue(BridgeFrame.TryDecode(buffer, out BridgeFrame decoded));
        Assert.AreEqual(source.Command, decoded.Command);
        Assert.AreEqual(source.Sequence, decoded.Sequence);
    }
}
