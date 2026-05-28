using System.Runtime.CompilerServices;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ReusableArchiveBZip2Decompressor
{
    private sealed partial class BZip2Workspace
    {
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

    }
}
