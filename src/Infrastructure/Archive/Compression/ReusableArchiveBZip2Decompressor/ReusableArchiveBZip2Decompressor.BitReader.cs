using System.Runtime.CompilerServices;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ReusableArchiveBZip2Decompressor
{
    private sealed partial class BZip2Workspace
    {
        private sealed class BitReader
        {
            private byte[]? buffer;
            private int length;
            private int position;
            private ulong bits;
            private int bitCount;

            public void Reset(byte[] source, int sourceLength)
            {
                buffer = source;
                length = sourceLength;
                position = 0;
                bits = 0;
                bitCount = 0;
            }

            public int ReadBit() => ReadBits(1);

            public void SkipBits(int count)
            {
                while (count > 0)
                {
                    var chunk = Math.Min(count, 24);
                    ReadBits(chunk);
                    count -= chunk;
                }
            }

            public long ReadBits48()
            {
                long value = 0;
                for (var i = 0; i < 6; i++)
                {
                    value = (value << 8) | (uint)ReadBits(8);
                }

                return value;
            }

            public int ReadBits(int count)
            {
                if (count is <= 0 or > 32)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                while (bitCount < count)
                {
                    if (position >= length)
                    {
                        throw new InvalidDataException("Unexpected end of BZip2 stream.");
                    }

                    bits = (bits << 8) | buffer![position++];
                    bitCount += 8;
                }

                bitCount -= count;
                var value = (int)((bits >> bitCount) & ((1UL << count) - 1));
                bits = bitCount == 0 ? 0 : bits & ((1UL << bitCount) - 1);
                return value;
            }
        }
    }
}
