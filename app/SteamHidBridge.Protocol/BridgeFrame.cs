using System;

namespace SteamHidBridge.Protocol;

/// <summary>
/// Represents one validated bridge transport frame.
/// </summary>
/// <param name="Command">The bridge command carried by the frame.</param>
/// <param name="Sequence">The sender-managed frame sequence number.</param>
/// <param name="Payload">The payload bytes for the command.</param>
public readonly record struct BridgeFrame(BridgeCommand Command, byte Sequence, byte[] Payload)
{
    /// <summary>
    /// Gets the current wire-format version.
    /// </summary>
    public const byte Version = 1;

    /// <summary>
    /// Gets the number of bytes in the fixed frame header.
    /// </summary>
    public const int HeaderSize = 7;

    /// <summary>
    /// Gets the number of bytes in the trailing checksum.
    /// </summary>
    public const int ChecksumSize = 2;

    /// <summary>
    /// Gets the maximum supported payload length in bytes.
    /// </summary>
    public const int MaxPayloadLength = 64;

    private const byte Magic0 = (byte)'S';
    private const byte Magic1 = (byte)'H';
    private const byte Magic2 = (byte)'B';

    /// <summary>
    /// Gets the total encoded frame size in bytes.
    /// </summary>
    public int WireSize => HeaderSize + Payload.Length + ChecksumSize;

    /// <summary>
    /// Encodes the frame into a new byte array.
    /// </summary>
    /// <returns>The encoded frame bytes.</returns>
    /// <exception cref="InvalidOperationException">The frame could not be encoded.</exception>
    public byte[] Encode()
    {
        byte[] buffer = new byte[WireSize];
        return TryWrite(buffer, out _) ? buffer : throw new InvalidOperationException("Frame could not be encoded.");
    }

    /// <summary>
    /// Writes the encoded frame into the supplied destination buffer.
    /// </summary>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="bytesWritten">The number of bytes written when encoding succeeds.</param>
    /// <returns><see langword="true"/> when the frame was encoded; otherwise, <see langword="false"/>.</returns>
    public bool TryWrite(Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;
        if (Payload.Length > MaxPayloadLength || destination.Length < WireSize)
        {
            return false;
        }

        destination[0] = Magic0;
        destination[1] = Magic1;
        destination[2] = Magic2;
        destination[3] = Version;
        destination[4] = (byte)Command;
        destination[5] = Sequence;
        destination[6] = (byte)Payload.Length;
        Payload.CopyTo(destination[HeaderSize..]);

        int checksumOffset = HeaderSize + Payload.Length;
        ushort checksum = ComputeChecksum16(destination[..checksumOffset]);
        destination[checksumOffset] = (byte)checksum;
        destination[checksumOffset + 1] = (byte)(checksum >> 8);
        bytesWritten = checksumOffset + ChecksumSize;
        return true;
    }

    /// <summary>
    /// Attempts to decode a complete bridge frame from the supplied bytes.
    /// </summary>
    /// <param name="data">The candidate encoded frame bytes.</param>
    /// <param name="frame">The decoded frame when the input is valid.</param>
    /// <returns><see langword="true"/> when decoding succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryDecode(ReadOnlySpan<byte> data, out BridgeFrame frame)
    {
        frame = default;

        if (data.Length < HeaderSize + ChecksumSize ||
            data[0] != Magic0 ||
            data[1] != Magic1 ||
            data[2] != Magic2 ||
            data[3] != Version)
        {
            return false;
        }

        int payloadLength = data[6];
        if (data[4] != (byte)BridgeCommand.HidInput ||
            payloadLength > MaxPayloadLength ||
            data.Length != HeaderSize + payloadLength + ChecksumSize)
        {
            return false;
        }

        ushort expected = ComputeChecksum16(data[..(HeaderSize + payloadLength)]);
        ushort actual = unchecked((ushort)(data[^2] | (data[^1] << 8)));
        if (expected != actual)
        {
            return false;
        }

        byte[] payload = data.Slice(HeaderSize, payloadLength).ToArray();
        frame = new BridgeFrame((BridgeCommand)data[4], data[5], payload);
        return true;
    }

    private static ushort ComputeChecksum16(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        foreach (byte value in data)
        {
            sum += value;
        }

        return unchecked((ushort)sum);
    }
}
