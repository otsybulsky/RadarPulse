using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

internal sealed class ArchiveTwoRadarEventBatchProjector : IArchiveTwoMessageConsumer
{
    private const int MessageHeaderLength = 16;
    private const int Type31DataHeaderMinimumLength = 72;
    private const int Type31DataBlockPointerOffset = 32;
    private const int Type31DataBlockPointerLength = 4;
    private const int Type31MaximumDataBlockPointers = 10;
    private const int GenericMomentDescriptorLength = 28;
    private const int GenericMomentDataOffset = 28;
    private const int GenericMomentGateCountOffset = 8;
    private const int GenericMomentWordSizeOffset = 19;
    private const int GenericMomentScaleOffset = 20;
    private const int GenericMomentOffsetOffset = 24;
    private const int DefaultInitialEventCapacity = 256;
    private const int DefaultInitialPayloadCapacity = 4096;

    private readonly RadarSourceUniverse sourceUniverse;
    private readonly RadarStreamIdentityNormalizer identityNormalizer;
    private readonly byte[] radarIdUtf8;
    private readonly Dictionary<int, CachedIdentityDimensions> identityCacheByMomentCode = new();
    private readonly RadarEventBatchBuilder batchBuilder;
    private DictionaryVersion dictionarySnapshotVersion = DictionaryVersion.Initial;
    private int radialSequenceNumber;

    public ArchiveTwoRadarEventBatchProjector(
        string radarId,
        DateTimeOffset volumeTimestamp,
        RadarSourceUniverse sourceUniverse,
        int initialEventCapacity = DefaultInitialEventCapacity,
        int initialPayloadCapacity = DefaultInitialPayloadCapacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(radarId);
        ArgumentNullException.ThrowIfNull(sourceUniverse);

        RadarId = radarId;
        VolumeTimestamp = volumeTimestamp;
        this.sourceUniverse = sourceUniverse;
        radarIdUtf8 = Encoding.ASCII.GetBytes(radarId);
        identityNormalizer = new RadarStreamIdentityNormalizer(sourceUniverse);
        batchBuilder = new RadarEventBatchBuilder(initialEventCapacity, initialPayloadCapacity);
    }

    public string RadarId { get; }

    public DateTimeOffset VolumeTimestamp { get; }

    public RadarStreamDictionarySnapshot DictionarySnapshot =>
        identityNormalizer.CreateDictionarySnapshot(dictionarySnapshotVersion);

    public RadarEventBatch BuildBatch()
    {
        var batch = batchBuilder.BuildAndReset();
        if (batch.DictionaryVersion.Value > dictionarySnapshotVersion.Value)
        {
            dictionarySnapshotVersion = batch.DictionaryVersion;
        }

        return batch;
    }

    public void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source)
    {
        if (message.Length < MessageHeaderLength || message[3] != 31)
        {
            return;
        }

        ParseType31(message[MessageHeaderLength..], source);
    }

    private void ParseType31(ReadOnlySpan<byte> payload, ArchiveTwoMessageSource source)
    {
        if (payload.Length < Type31DataHeaderMinimumLength)
        {
            return;
        }

        var radialLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(18, 2));
        var parseLength = Math.Min(payload.Length, radialLength > 0 ? radialLength : payload.Length);
        var parsePayload = payload[..parseLength];
        radialSequenceNumber++;

        var elevationSlot = Math.Max(parsePayload[22] - 1, 0);
        var azimuthBucket = GetAzimuthBucket(radialSequenceNumber);
        var blockCount = BinaryPrimitives.ReadUInt16BigEndian(parsePayload.Slice(30, 2));
        var pointerCount = Math.Min(
            Math.Min((int)blockCount, Type31MaximumDataBlockPointers),
            (parsePayload.Length - Type31DataBlockPointerOffset) / Type31DataBlockPointerLength);

        for (var i = 0; i < pointerCount; i++)
        {
            var pointer = BinaryPrimitives.ReadInt32BigEndian(
                parsePayload.Slice(Type31DataBlockPointerOffset + i * Type31DataBlockPointerLength, Type31DataBlockPointerLength));
            if (pointer <= 0 || pointer >= parsePayload.Length)
            {
                continue;
            }

            var block = parsePayload[pointer..];
            if (block.Length < GenericMomentDescriptorLength || block[0] != (byte)'D')
            {
                continue;
            }

            AcceptMomentBlock(block, source, elevationSlot, azimuthBucket);
        }
    }

    private int GetAzimuthBucket(int radialSequence)
    {
        // First integration uses deterministic radial order buckets. A later
        // parser slice can swap in decoded azimuth angle without changing the
        // stream contract or SourceId arithmetic.
        return (radialSequence - 1) % sourceUniverse.AzimuthBucketCount;
    }

    private void AcceptMomentBlock(
        ReadOnlySpan<byte> block,
        ArchiveTwoMessageSource source,
        int elevationSlot,
        int azimuthBucket)
    {
        var momentName = ReadDataBlockNameUtf8(block);
        if (momentName.Length == 0)
        {
            return;
        }

        var metadata = ReadMomentMetadata(block);
        var wordSize = metadata.WordSizeBits switch
        {
            8 => RadarStreamWordSize.EightBit,
            16 => RadarStreamWordSize.SixteenBit,
            _ => default
        };
        if (wordSize == 0)
        {
            return;
        }

        var bytesPerGate = wordSize == RadarStreamWordSize.EightBit ? 1 : sizeof(ushort);
        var requiredBytes = checked(metadata.GateCount * bytesPerGate);
        var data = block[GenericMomentDataOffset..];
        if (data.Length < requiredBytes)
        {
            return;
        }

        var rangeBandCount = sourceUniverse.RangeBandCount;
        for (var rangeBand = 0; rangeBand < rangeBandCount; rangeBand++)
        {
            var startGate = rangeBand * metadata.GateCount / rangeBandCount;
            var endGate = (rangeBand + 1) * metadata.GateCount / rangeBandCount;
            if (endGate <= startGate)
            {
                continue;
            }

            if (startGate > ushort.MaxValue || endGate - startGate > ushort.MaxValue)
            {
                throw new InvalidDataException("Type 31 moment gate run exceeds the stream event range.");
            }

            var identity = ResolveIdentity(momentName, elevationSlot, azimuthBucket, rangeBand);
            var payloadOffset = startGate * bytesPerGate;
            var payloadLength = (endGate - startGate) * bytesPerGate;
            batchBuilder.AddEvent(
                identity,
                VolumeTimestamp.UtcTicks,
                source.MessageTimestamp.UtcTicks,
                source.CompressedRecordSequenceNumber,
                source.MessageSequenceNumberInRecord,
                radialSequenceNumber,
                checked((ushort)startGate),
                checked((ushort)(endGate - startGate)),
                wordSize,
                metadata.Scale,
                metadata.Offset,
                RadarStreamStatusModel.ArchiveTwoMoment,
                data.Slice(payloadOffset, payloadLength));
        }
    }

    private RadarStreamIdentity ResolveIdentity(
        ReadOnlySpan<byte> momentNameUtf8,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        var momentCode = GetMomentCode(momentNameUtf8);
        if (!identityCacheByMomentCode.TryGetValue(momentCode, out var dimensions))
        {
            return ResolveIdentityAndCacheDimensions(
                momentCode,
                momentNameUtf8,
                elevationSlot,
                azimuthBucket,
                rangeBand);
        }

        return CreateIdentityFromCachedDimensions(
            dimensions,
            elevationSlot,
            azimuthBucket,
            rangeBand);
    }

    private RadarStreamIdentity ResolveIdentityAndCacheDimensions(
        int momentCode,
        ReadOnlySpan<byte> momentNameUtf8,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        var result = identityNormalizer.TryNormalize(
            radarIdUtf8,
            momentNameUtf8,
            elevationSlot,
            azimuthBucket,
            rangeBand);
        if (result.IsResolved)
        {
            CacheIdentityDimensions(momentCode, result.Identity);
            return result.Identity;
        }

        throw new InvalidDataException($"Failed to normalize radar stream identity: {result.Error}.");
    }

    private void CacheIdentityDimensions(int momentCode, RadarStreamIdentity identity)
    {
        if (identityCacheByMomentCode.ContainsKey(momentCode))
        {
            return;
        }

        identityCacheByMomentCode.Add(
            momentCode,
            new CachedIdentityDimensions(
                identity.RadarOrdinal,
                identity.MomentId,
                sourceUniverse.GetRadarSourceBlockStart(identity.RadarOrdinal)));
    }

    private RadarStreamIdentity CreateIdentityFromCachedDimensions(
        CachedIdentityDimensions dimensions,
        int elevationSlot,
        int azimuthBucket,
        int rangeBand)
    {
        if ((uint)elevationSlot >= (uint)sourceUniverse.ElevationSlotCount ||
            (uint)azimuthBucket >= (uint)sourceUniverse.AzimuthBucketCount ||
            (uint)rangeBand >= (uint)sourceUniverse.RangeBandCount)
        {
            throw new InvalidDataException("Radar stream source dimensions are outside the source universe.");
        }

        if (elevationSlot > ushort.MaxValue ||
            azimuthBucket > ushort.MaxValue ||
            rangeBand > ushort.MaxValue)
        {
            throw new InvalidDataException("Radar stream source dimensions exceed the stream event range.");
        }

        var sourceId =
            dimensions.RadarSourceBlockStart +
            (elevationSlot * sourceUniverse.SourcesPerElevationSlot) +
            (azimuthBucket * sourceUniverse.SourcesPerAzimuthBucket) +
            rangeBand;

        return new RadarStreamIdentity(
            sourceId,
            dimensions.RadarOrdinal,
            dimensions.MomentId,
            checked((ushort)elevationSlot),
            checked((ushort)azimuthBucket),
            checked((ushort)rangeBand),
            identityNormalizer.CurrentDictionaryVersion,
            identityNormalizer.SourceUniverseVersion);
    }

    private static int GetMomentCode(ReadOnlySpan<byte> momentNameUtf8)
    {
        var code = momentNameUtf8.Length << 24;
        for (var i = 0; i < momentNameUtf8.Length; i++)
        {
            code |= momentNameUtf8[i] << (i * 8);
        }

        return code;
    }

    private static ReadOnlySpan<byte> ReadDataBlockNameUtf8(ReadOnlySpan<byte> block)
    {
        var name = block.Slice(1, 3);
        var length = name.Length;
        while (length > 0 && name[length - 1] is 0 or (byte)' ')
        {
            length--;
        }

        return name[..length];
    }

    private static Type31MomentMetadata ReadMomentMetadata(ReadOnlySpan<byte> block) =>
        new(
            BinaryPrimitives.ReadUInt16BigEndian(block.Slice(GenericMomentGateCountOffset, 2)),
            block[GenericMomentWordSizeOffset],
            ReadSingleBigEndian(block.Slice(GenericMomentScaleOffset, 4)),
            ReadSingleBigEndian(block.Slice(GenericMomentOffsetOffset, 4)));

    private static float ReadSingleBigEndian(ReadOnlySpan<byte> buffer) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(buffer));

    private readonly record struct Type31MomentMetadata(
        int GateCount,
        int WordSizeBits,
        float Scale,
        float Offset);

    private readonly record struct CachedIdentityDimensions(
        ushort RadarOrdinal,
        ushort MomentId,
        int RadarSourceBlockStart);
}
