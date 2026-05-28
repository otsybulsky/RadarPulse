using System.Runtime.CompilerServices;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ReusableArchiveBZip2Decompressor
{
    private sealed partial class BZip2Workspace
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

    }
}
