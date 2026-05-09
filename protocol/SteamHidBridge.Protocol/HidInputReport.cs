using System;
using System.Buffers.Binary;

namespace SteamHidBridge.Protocol;

public readonly record struct HidInputReport(
    short PointerDeltaX,
    short PointerDeltaY,
    sbyte VerticalWheel,
    MouseButtons MouseButtons,
    KeyboardModifiers KeyboardModifiers,
    byte KeyboardUsageId)
{
    public const int WireSize = 9;

    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < WireSize)
        {
            throw new ArgumentException("Destination is too small.", nameof(destination));
        }

        BinaryPrimitives.WriteInt16LittleEndian(destination, PointerDeltaX);
        BinaryPrimitives.WriteInt16LittleEndian(destination[2..], PointerDeltaY);
        destination[4] = unchecked((byte)VerticalWheel);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[5..], (ushort)MouseButtons);
        destination[7] = (byte)KeyboardModifiers;
        destination[8] = KeyboardUsageId;
    }

    public static HidInputReport ReadFrom(ReadOnlySpan<byte> source)
    {
        return source.Length < WireSize
            ? throw new ArgumentException("Source is too small.", nameof(source))
            : new HidInputReport(
            BinaryPrimitives.ReadInt16LittleEndian(source),
            BinaryPrimitives.ReadInt16LittleEndian(source[2..]),
            unchecked((sbyte)source[4]),
            (MouseButtons)BinaryPrimitives.ReadUInt16LittleEndian(source[5..]),
            (KeyboardModifiers)source[7],
            source[8]);
    }
}
