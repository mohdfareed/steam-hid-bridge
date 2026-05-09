using System;

namespace SteamHidBridge.Protocol;

public static class Checksum16
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        foreach (byte value in data)
        {
            sum += value;
        }

        return unchecked((ushort)sum);
    }
}
