using System;
using System.Buffers.Binary;

namespace SteamHidBridge.Protocol;

/// <summary>
/// Defines bridge protocol command identifiers.
/// </summary>
public enum BridgeCommand : byte
{
    /// <summary>
    /// Carries one HID input report.
    /// </summary>
    HidInput = 0x10
}

/// <summary>
/// Defines the mouse buttons that can be emitted by the bridge.
/// </summary>
[Flags]
public enum MouseButtons : ushort
{
    /// <summary>No button is pressed.</summary>
    None = 0,
    /// <summary>The left mouse button.</summary>
    Left = 1 << 0,
    /// <summary>The right mouse button.</summary>
    Right = 1 << 1,
    /// <summary>The middle mouse button.</summary>
    Middle = 1 << 2,
    /// <summary>The first side button.</summary>
    Back = 1 << 3,
    /// <summary>The second side button.</summary>
    Forward = 1 << 4
}

/// <summary>
/// Represents the fixed-size HID payload exchanged between the app and the board.
/// </summary>
/// <param name="PointerDeltaX">The relative mouse delta on the X axis.</param>
/// <param name="PointerDeltaY">The relative mouse delta on the Y axis.</param>
/// <param name="VerticalWheel">The signed vertical wheel delta.</param>
/// <param name="MouseButtons">The pressed mouse buttons.</param>
public readonly record struct HidInputReport(
    short PointerDeltaX,
    short PointerDeltaY,
    sbyte VerticalWheel,
    MouseButtons MouseButtons)
{
    /// <summary>
    /// Gets the encoded report size in bytes.
    /// </summary>
    public const int WireSize = 9;

    /// <summary>
    /// Writes the report into the supplied destination buffer.
    /// </summary>
    /// <param name="destination">The destination buffer.</param>
    /// <exception cref="ArgumentException">The destination buffer is too small.</exception>
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
    }

    /// <summary>
    /// Reads a report from the supplied source buffer.
    /// </summary>
    /// <param name="source">The encoded report bytes.</param>
    /// <returns>The decoded report.</returns>
    /// <exception cref="ArgumentException">The source buffer is too small.</exception>
    public static HidInputReport ReadFrom(ReadOnlySpan<byte> source)
    {
        return source.Length < WireSize
            ? throw new ArgumentException("Source is too small.", nameof(source))
            : new HidInputReport(
                BinaryPrimitives.ReadInt16LittleEndian(source),
                BinaryPrimitives.ReadInt16LittleEndian(source[2..]),
                unchecked((sbyte)source[4]),
                (MouseButtons)BinaryPrimitives.ReadUInt16LittleEndian(source[5..]));
    }
}
