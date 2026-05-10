using System.Runtime.CompilerServices;

namespace RadarPulse.Infrastructure.Archive;

public sealed class ReusableArchiveBZip2Decompressor : IArchiveBZip2Decompressor
{
    public const string DecompressorName = "radarpulse";

    public string Name => DecompressorName;

    public IArchiveBZip2DecompressionSession CreateSession() => new Session();

    public long Decompress(
        byte[] compressedPayload,
        int compressedSizeBytes,
        byte[] outputBuffer,
        ArchiveBZip2DecompressedChunkHandler? chunkHandler) =>
        CreateSession().Decompress(compressedPayload, compressedSizeBytes, outputBuffer, chunkHandler);

    public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
        CreateSession().CountDecompressedBytes(compressedPayload, compressedSizeBytes, outputBuffer);

    private sealed class Session : IArchiveBZip2DecompressionSession
    {
        private readonly BZip2Workspace workspace = new();

        public long Decompress(
            byte[] compressedPayload,
            int compressedSizeBytes,
            byte[] outputBuffer,
            ArchiveBZip2DecompressedChunkHandler? chunkHandler) =>
            workspace.Decompress(compressedPayload, compressedSizeBytes, outputBuffer, chunkHandler);

        public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
            Decompress(compressedPayload, compressedSizeBytes, outputBuffer, null);
    }

    private sealed class BZip2Workspace
    {
        private const long BlockHeaderMarker = 0x314159265359;
        private const long EndHeaderMarker = 0x177245385090;
        private const int BaseBlockSize = 100_000;
        private const int MaxBlockSize = 900_000;
        private const int MaxAlphaSize = 258;
        private const int MaxGroups = 6;
        private const int MaxSelectors = 18_002;
        private const int GroupRunLength = 50;
        private const int MaxCodeLength = 23;
        private const int CodeTableLength = MaxCodeLength + 2;
        private const int CrcPolynomial = 0x04C11DB7;

        private static readonly int[] CrcTable = BuildCrcTable();

        private readonly byte[] inUse = new byte[256];
        private readonly byte[] seqToUnseq = new byte[256];
        private readonly byte[] mtfSymbols = new byte[256];
        private readonly byte[] selectorMtf = new byte[MaxSelectors];
        private readonly byte[] selector = new byte[MaxSelectors];
        private readonly byte[] codeLengths = new byte[MaxGroups * MaxAlphaSize];
        private readonly int[] limit = new int[MaxGroups * CodeTableLength];
        private readonly int[] codeBase = new int[MaxGroups * CodeTableLength];
        private readonly int[] perm = new int[MaxGroups * MaxAlphaSize];
        private readonly int[] minLens = new int[MaxGroups];
        private readonly int[] maxLens = new int[MaxGroups];
        private readonly int[] unzftab = new int[256];
        private readonly int[] cftab = new int[257];
        private readonly byte[] ll8 = new byte[MaxBlockSize];
        private readonly int[] tt = new int[MaxBlockSize];

        private readonly BitReader reader = new();
        private int blockSizeLimit;
        private int alphaSize;
        private int eobSymbol;
        private int groupCount;
        private int selectorCount;
        private int groupNumber;
        private int groupPosition;
        private int currentGroup;
        private int storedBlockCrc;
        private int computedCombinedCrc;

        public long Decompress(
            byte[] compressedPayload,
            int compressedSizeBytes,
            byte[] outputBuffer,
            ArchiveBZip2DecompressedChunkHandler? chunkHandler)
        {
            ArgumentNullException.ThrowIfNull(compressedPayload);
            ArgumentNullException.ThrowIfNull(outputBuffer);
            if (compressedSizeBytes < 0 || compressedSizeBytes > compressedPayload.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(compressedSizeBytes));
            }

            if (outputBuffer.Length == 0)
            {
                throw new ArgumentException("Output buffer must not be empty.", nameof(outputBuffer));
            }

            reader.Reset(compressedPayload, compressedSizeBytes);
            ReadStreamHeader();

            computedCombinedCrc = 0;
            long decompressedBytes = 0;
            while (true)
            {
                var marker = reader.ReadBits48();
                if (marker == EndHeaderMarker)
                {
                    var storedCombinedCrc = reader.ReadBits(32);
                    if (storedCombinedCrc != computedCombinedCrc)
                    {
                        throw new InvalidDataException("BZip2 combined CRC mismatch.");
                    }

                    return decompressedBytes;
                }

                if (marker != BlockHeaderMarker)
                {
                    throw new InvalidDataException("BZip2 stream contains an unsupported block marker.");
                }

                storedBlockCrc = reader.ReadBits(32);
                if (reader.ReadBit() != 0)
                {
                    throw new InvalidDataException("Randomized BZip2 blocks are not supported.");
                }

                var originalPointer = reader.ReadBits(24);
                var last = DecodeBlock();
                if (originalPointer < 0 || originalPointer > last)
                {
                    throw new InvalidDataException("BZip2 block original pointer is outside the decoded block.");
                }

                decompressedBytes += InverseBlock(last, originalPointer, outputBuffer, chunkHandler);
            }
        }

        private void ReadStreamHeader()
        {
            if (reader.ReadBits(8) != 'B' ||
                reader.ReadBits(8) != 'Z' ||
                reader.ReadBits(8) != 'h')
            {
                throw new InvalidDataException("Compressed payload does not start with a BZip2 stream header.");
            }

            var blockSizeCharacter = reader.ReadBits(8);
            if (blockSizeCharacter < '1' || blockSizeCharacter > '9')
            {
                throw new InvalidDataException("BZip2 stream has an unsupported block size.");
            }

            blockSizeLimit = (blockSizeCharacter - '0') * BaseBlockSize;
        }

        private int DecodeBlock()
        {
            Array.Clear(inUse);
            Array.Clear(unzftab);

            Span<byte> inUseGroups = stackalloc byte[16];
            for (var group = 0; group < inUseGroups.Length; group++)
            {
                inUseGroups[group] = (byte)reader.ReadBit();
            }

            for (var group = 0; group < inUseGroups.Length; group++)
            {
                if (inUseGroups[group] == 0)
                {
                    continue;
                }

                var baseSymbol = group * 16;
                for (var bit = 0; bit < 16; bit++)
                {
                    if (reader.ReadBit() != 0)
                    {
                        inUse[baseSymbol + bit] = 1;
                    }
                }
            }

            var inUseCount = 0;
            for (var symbol = 0; symbol < inUse.Length; symbol++)
            {
                if (inUse[symbol] != 0)
                {
                    seqToUnseq[inUseCount++] = (byte)symbol;
                }
            }

            if (inUseCount == 0)
            {
                throw new InvalidDataException("BZip2 block does not declare any in-use symbols.");
            }

            alphaSize = inUseCount + 2;
            eobSymbol = inUseCount + 1;
            groupCount = reader.ReadBits(3);
            if (groupCount is < 2 or > MaxGroups)
            {
                throw new InvalidDataException("BZip2 block has an unsupported Huffman group count.");
            }

            selectorCount = reader.ReadBits(15);
            if (selectorCount is <= 0 or > MaxSelectors)
            {
                throw new InvalidDataException("BZip2 block has an unsupported selector count.");
            }

            ReadSelectors();
            ReadHuffmanTables();

            for (var i = 0; i < inUseCount; i++)
            {
                mtfSymbols[i] = (byte)i;
            }

            groupNumber = -1;
            groupPosition = 0;
            currentGroup = 0;

            var last = -1;
            var nextSymbol = ReadNextSymbol();
            while (nextSymbol != eobSymbol)
            {
                if (nextSymbol is 0 or 1)
                {
                    var runLength = -1;
                    var runPower = 1;
                    do
                    {
                        runLength += (nextSymbol + 1) * runPower;
                        if (runPower > int.MaxValue / 2)
                        {
                            throw new InvalidDataException("BZip2 run length is too large.");
                        }

                        runPower <<= 1;
                        nextSymbol = ReadNextSymbol();
                    }
                    while (nextSymbol is 0 or 1);

                    runLength++;
                    if (runLength <= 0 || last + runLength >= blockSizeLimit)
                    {
                        throw new InvalidDataException("BZip2 block exceeds the declared block size.");
                    }

                    var repeated = seqToUnseq[mtfSymbols[0]];
                    unzftab[repeated] += runLength;
                    for (var i = 0; i < runLength; i++)
                    {
                        ll8[++last] = repeated;
                    }

                    continue;
                }

                var mtfIndex = nextSymbol - 1;
                if (mtfIndex < 0 || mtfIndex >= inUseCount)
                {
                    throw new InvalidDataException("BZip2 block contains an invalid MTF symbol.");
                }

                var mtfSymbol = MoveToFront(mtfIndex);
                var decoded = seqToUnseq[mtfSymbol];
                unzftab[decoded]++;
                if (++last >= blockSizeLimit)
                {
                    throw new InvalidDataException("BZip2 block exceeds the declared block size.");
                }

                ll8[last] = decoded;
                nextSymbol = ReadNextSymbol();
            }

            return last;
        }

        private void ReadSelectors()
        {
            for (var i = 0; i < selectorCount; i++)
            {
                var mtfIndex = 0;
                while (reader.ReadBit() != 0)
                {
                    mtfIndex++;
                    if (mtfIndex >= groupCount)
                    {
                        throw new InvalidDataException($"BZip2 selector MTF index {mtfIndex} is outside the group table with {groupCount} groups and {selectorCount} selectors.");
                    }
                }

                selectorMtf[i] = (byte)mtfIndex;
            }

            Span<byte> groups = stackalloc byte[MaxGroups];
            for (var i = 0; i < groupCount; i++)
            {
                groups[i] = (byte)i;
            }

            for (var i = 0; i < selectorCount; i++)
            {
                var mtfIndex = selectorMtf[i];
                var value = groups[mtfIndex];
                for (var j = mtfIndex; j > 0; j--)
                {
                    groups[j] = groups[j - 1];
                }

                groups[0] = value;
                selector[i] = value;
            }
        }

        private void ReadHuffmanTables()
        {
            for (var table = 0; table < groupCount; table++)
            {
                var currentLength = reader.ReadBits(5);
                var lengthOffset = table * MaxAlphaSize;
                for (var i = 0; i < alphaSize; i++)
                {
                    while (reader.ReadBit() != 0)
                    {
                        currentLength += reader.ReadBit() == 0 ? 1 : -1;
                        if (currentLength is <= 0 or > MaxCodeLength)
                        {
                            throw new InvalidDataException("BZip2 Huffman code length is outside the supported range.");
                        }
                    }

                    codeLengths[lengthOffset + i] = (byte)currentLength;
                }

                BuildDecodeTable(table);
            }
        }

        private void BuildDecodeTable(int table)
        {
            var lengthOffset = table * MaxAlphaSize;
            var codeOffset = table * CodeTableLength;
            var permOffset = table * MaxAlphaSize;

            Array.Clear(limit, codeOffset, CodeTableLength);
            Array.Clear(codeBase, codeOffset, CodeTableLength);

            var minLength = int.MaxValue;
            var maxLength = 0;
            for (var i = 0; i < alphaSize; i++)
            {
                var length = codeLengths[lengthOffset + i];
                minLength = Math.Min(minLength, length);
                maxLength = Math.Max(maxLength, length);
                codeBase[codeOffset + length + 1]++;
            }

            if (minLength == int.MaxValue || maxLength > MaxCodeLength)
            {
                throw new InvalidDataException("BZip2 Huffman table is invalid.");
            }

            for (var i = 1; i < CodeTableLength; i++)
            {
                codeBase[codeOffset + i] += codeBase[codeOffset + i - 1];
            }

            var permIndex = 0;
            for (var length = minLength; length <= maxLength; length++)
            {
                for (var symbol = 0; symbol < alphaSize; symbol++)
                {
                    if (codeLengths[lengthOffset + symbol] == length)
                    {
                        perm[permOffset + permIndex++] = symbol;
                    }
                }
            }

            var code = 0;
            for (var length = minLength; length <= maxLength; length++)
            {
                code += codeBase[codeOffset + length + 1] - codeBase[codeOffset + length];
                limit[codeOffset + length] = code - 1;
                code <<= 1;
            }

            for (var length = minLength + 1; length <= maxLength; length++)
            {
                codeBase[codeOffset + length] =
                    ((limit[codeOffset + length - 1] + 1) << 1) - codeBase[codeOffset + length];
            }

            minLens[table] = minLength;
            maxLens[table] = maxLength;
        }

        private int ReadNextSymbol()
        {
            if (groupPosition == 0)
            {
                groupNumber++;
                if (groupNumber >= selectorCount)
                {
                    throw new InvalidDataException("BZip2 block ran out of Huffman selectors.");
                }

                groupPosition = GroupRunLength;
                currentGroup = selector[groupNumber];
            }

            groupPosition--;
            var codeOffset = currentGroup * CodeTableLength;
            var permOffset = currentGroup * MaxAlphaSize;
            var codeLength = minLens[currentGroup];
            var code = reader.ReadBits(codeLength);

            while (codeLength <= maxLens[currentGroup] && code > limit[codeOffset + codeLength])
            {
                codeLength++;
                code = (code << 1) | reader.ReadBit();
            }

            if (codeLength > maxLens[currentGroup])
            {
                throw new InvalidDataException("BZip2 Huffman code is invalid.");
            }

            var permIndex = code - codeBase[codeOffset + codeLength];
            if (permIndex < 0 || permIndex >= alphaSize)
            {
                throw new InvalidDataException("BZip2 Huffman table produced an invalid symbol.");
            }

            return perm[permOffset + permIndex];
        }

        private byte MoveToFront(int index)
        {
            var value = mtfSymbols[index];
            for (var i = index; i > 0; i--)
            {
                mtfSymbols[i] = mtfSymbols[i - 1];
            }

            mtfSymbols[0] = value;
            return value;
        }

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
