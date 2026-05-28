using System.Runtime.CompilerServices;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ReusableArchiveBZip2Decompressor
{
    private sealed partial class BZip2Workspace
    {
        private long InverseBlock(
            int last,
            int originalPointer,
            byte[] outputBuffer,
            ArchiveBZip2DecompressedChunkHandler? chunkHandler)
        {
            if (last < 0)
            {
                return 0;
            }

            Array.Clear(cftab);
            for (var i = 1; i < cftab.Length; i++)
            {
                cftab[i] = unzftab[i - 1];
            }

            for (var i = 1; i < cftab.Length; i++)
            {
                cftab[i] += cftab[i - 1];
            }

            for (var i = 0; i <= last; i++)
            {
                var symbol = ll8[i];
                tt[cftab[symbol]++] = i;
            }

            var position = tt[originalPointer];
            var remaining = last + 1;
            var previous = -1;
            var repeatCount = 0;
            var outputPosition = 0;
            var blockCrc = unchecked((int)0xFFFFFFFF);
            long totalBytes = 0;

            while (remaining > 0)
            {
                var current = ll8[position];
                position = tt[position];
                remaining--;

                WriteDecodedByte(outputBuffer, ref outputPosition, ref blockCrc, current, chunkHandler);
                totalBytes++;

                if (current != previous)
                {
                    previous = current;
                    repeatCount = 1;
                    continue;
                }

                repeatCount++;
                if (repeatCount != 4)
                {
                    continue;
                }

                if (remaining <= 0)
                {
                    throw new InvalidDataException("BZip2 block ended inside a run-length sequence.");
                }

                var extraCount = ll8[position];
                position = tt[position];
                remaining--;
                for (var i = 0; i < extraCount; i++)
                {
                    WriteDecodedByte(outputBuffer, ref outputPosition, ref blockCrc, current, chunkHandler);
                }

                totalBytes += extraCount;
                previous = current;
                repeatCount = 0;
            }

            blockCrc = ~blockCrc;
            if (blockCrc != storedBlockCrc)
            {
                throw new InvalidDataException("BZip2 block CRC mismatch.");
            }

            if (outputPosition > 0)
            {
                chunkHandler?.Invoke(outputBuffer.AsSpan(0, outputPosition));
            }

            computedCombinedCrc = RotateLeft(computedCombinedCrc) ^ blockCrc;
            return totalBytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteDecodedByte(
            byte[] outputBuffer,
            ref int outputPosition,
            ref int blockCrc,
            byte value,
            ArchiveBZip2DecompressedChunkHandler? chunkHandler)
        {
            outputBuffer[outputPosition++] = value;
            if (outputPosition == outputBuffer.Length)
            {
                chunkHandler?.Invoke(outputBuffer);
                outputPosition = 0;
            }

            blockCrc = UpdateCrc(blockCrc, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int UpdateCrc(int crc, byte value) =>
            (crc << 8) ^ CrcTable[((crc >> 24) ^ value) & 0xFF];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RotateLeft(int value) =>
            (value << 1) | (int)((uint)value >> 31);

        private static int[] BuildCrcTable()
        {
            var table = new int[256];
            for (var i = 0; i < table.Length; i++)
            {
                var value = i << 24;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & unchecked((int)0x80000000)) != 0
                        ? (value << 1) ^ CrcPolynomial
                        : value << 1;
                }

                table[i] = value;
            }

            return table;
        }

    }
}
