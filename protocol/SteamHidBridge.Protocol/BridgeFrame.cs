using System;

namespace SteamHidBridge.Protocol;

public readonly record struct BridgeFrame(BridgeCommand Command, byte Sequence, byte[] Payload)
{
    public const byte Version = 1;
    public const int HeaderSize = 7;
    public const int ChecksumSize = 2;
    public const int MaxPayloadLength = 64;

    private const byte Magic0 = (byte)'S';
    private const byte Magic1 = (byte)'H';
    private const byte Magic2 = (byte)'B';

    public int WireSize => HeaderSize + Payload.Length + ChecksumSize;

    public byte[] Encode()
    {
        byte[] buffer = new byte[WireSize];
        return TryWrite(buffer, out _) ? buffer : throw new InvalidOperationException("Frame could not be encoded.");
    }

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
        ushort checksum = Checksum16.Compute(destination[..checksumOffset]);
        destination[checksumOffset] = (byte)checksum;
        destination[checksumOffset + 1] = (byte)(checksum >> 8);
        bytesWritten = checksumOffset + ChecksumSize;
        return true;
    }

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

        ushort expected = Checksum16.Compute(data[..(HeaderSize + payloadLength)]);
        ushort actual = unchecked((ushort)(data[^2] | (data[^1] << 8)));
        if (expected != actual)
        {
            return false;
        }

        byte[] payload = data.Slice(HeaderSize, payloadLength).ToArray();
        frame = new BridgeFrame((BridgeCommand)data[4], data[5], payload);
        return true;
    }
}
