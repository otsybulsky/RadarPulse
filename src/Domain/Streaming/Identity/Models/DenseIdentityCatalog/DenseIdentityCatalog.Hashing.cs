using System.Numerics;
using System.Runtime.InteropServices;

namespace RadarPulse.Domain.Streaming;

public sealed partial class DenseIdentityCatalog
{
    private static uint GetUtf8Hash(ReadOnlySpan<byte> value)
    {
        unchecked
        {
            uint hash = 0;
            var remaining = value;

            while (remaining.Length >= sizeof(ulong))
            {
                hash = BitOperations.Crc32C(hash, MemoryMarshal.Read<ulong>(remaining));
                remaining = remaining[sizeof(ulong)..];
            }

            if (remaining.Length >= sizeof(uint))
            {
                hash = BitOperations.Crc32C(hash, MemoryMarshal.Read<uint>(remaining));
                remaining = remaining[sizeof(uint)..];
            }

            for (var i = 0; i < remaining.Length; i++)
            {
                hash = BitOperations.Crc32C(hash, remaining[i]);
            }

            return hash;
        }
    }

    private static int RoundUpPowerOfTwo(int value)
    {
        if (value <= 2)
        {
            return 2;
        }

        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}
