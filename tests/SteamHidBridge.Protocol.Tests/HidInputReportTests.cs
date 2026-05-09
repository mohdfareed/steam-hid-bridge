using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamHidBridge.Protocol;

namespace SteamHidBridge.Protocol.Tests;

[TestClass]
public sealed class HidInputReportTests
{
    [TestMethod]
    public void ReadFromReconstructsSyntheticMouseAndKeyboardReport()
    {
        var expected = new HidInputReport(
            PointerDeltaX: short.MinValue,
            PointerDeltaY: short.MaxValue,
            VerticalWheel: -1,
            MouseButtons: MouseButtons.Left | MouseButtons.Middle | MouseButtons.Forward,
            KeyboardModifiers: KeyboardModifiers.LeftShift | KeyboardModifiers.RightAlt,
            KeyboardUsageId: 0x04);
        Span<byte> wire = stackalloc byte[HidInputReport.WireSize];

        expected.WriteTo(wire);
        HidInputReport actual = HidInputReport.ReadFrom(wire);

        Assert.AreEqual(expected, actual);
    }
}
