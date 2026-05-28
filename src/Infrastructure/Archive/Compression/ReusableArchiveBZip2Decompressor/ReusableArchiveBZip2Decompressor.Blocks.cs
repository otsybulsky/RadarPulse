using System.Runtime.CompilerServices;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ReusableArchiveBZip2Decompressor
{
    private sealed partial class BZip2Workspace
    {
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

    }
}
