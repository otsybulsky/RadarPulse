using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Projects Archive II type 31 generic moment blocks into compact radar stream event batches.
/// </summary>
/// <remarks>
/// The projector uses a source universe for deterministic SourceId arithmetic and an identity normalizer for
/// dictionary-backed radar and moment ids.
/// </remarks>
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
    private readonly SourceUniverseVersion sourceUniverseVersion;
    private readonly int elevationSlotCount;
    private readonly int azimuthBucketCount;
    private readonly int rangeBandCount;
    private readonly int sourcesPerElevationSlot;
    private readonly int sourcesPerAzimuthBucket;
    private readonly Dictionary<int, CachedIdentityDimensions> identityCacheByMomentCode = new();
    private readonly RadarEventBatchBuilder batchBuilder;
    private RadarStreamIdentityNormalizer identityNormalizer;
    private string radarId = string.Empty;
    private byte[] radarIdUtf8 = [];
    private DateTimeOffset volumeTimestamp;
    private long volumeTimestampUtcTicks;
    private DictionaryVersion currentDictionaryVersion = DictionaryVersion.Initial;
    private DictionaryVersion dictionarySnapshotVersion = DictionaryVersion.Initial;
    private int radialSequenceNumber;

    /// <summary>
    /// Creates a batch projector for one Archive II volume and source universe.
    /// </summary>
    public ArchiveTwoRadarEventBatchProjector(
        string radarId,
        DateTimeOffset volumeTimestamp,
        RadarSourceUniverse sourceUniverse,
        int initialEventCapacity = DefaultInitialEventCapacity,
        int initialPayloadCapacity = DefaultInitialPayloadCapacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(radarId);
        ArgumentNullException.ThrowIfNull(sourceUniverse);

        this.sourceUniverse = sourceUniverse;
        sourceUniverseVersion = sourceUniverse.Version;
        elevationSlotCount = sourceUniverse.ElevationSlotCount;
        azimuthBucketCount = sourceUniverse.AzimuthBucketCount;
        rangeBandCount = sourceUniverse.RangeBandCount;
        sourcesPerElevationSlot = sourceUniverse.SourcesPerElevationSlot;
        sourcesPerAzimuthBucket = sourceUniverse.SourcesPerAzimuthBucket;
        identityNormalizer = new RadarStreamIdentityNormalizer(sourceUniverse);
        batchBuilder = new RadarEventBatchBuilder(initialEventCapacity, initialPayloadCapacity);
        ResetVolume(radarId, volumeTimestamp, initialEventCapacity, initialPayloadCapacity);
    }

    /// <summary>
    /// Gets the current radar id.
    /// </summary>
    public string RadarId => radarId;

    /// <summary>
    /// Gets the current Archive II volume timestamp.
    /// </summary>
    public DateTimeOffset VolumeTimestamp => volumeTimestamp;

    /// <summary>
    /// Gets the dictionary snapshot needed to resolve compact ids produced by built batches.
    /// </summary>
    public RadarStreamDictionarySnapshot DictionarySnapshot =>
        identityNormalizer.CreateDictionarySnapshot(dictionarySnapshotVersion);

    /// <summary>
    /// Resets the projector for a new Archive II volume while reusing buffers and dictionaries where possible.
    /// </summary>
    public void ResetVolume(
        string radarId,
        DateTimeOffset volumeTimestamp,
        int initialEventCapacity = DefaultInitialEventCapacity,
        int initialPayloadCapacity = DefaultInitialPayloadCapacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(radarId);

        var sameRadar = string.Equals(this.radarId, radarId, StringComparison.Ordinal);
        this.radarId = radarId;
        this.volumeTimestamp = volumeTimestamp;
        volumeTimestampUtcTicks = volumeTimestamp.UtcTicks;
        radialSequenceNumber = 0;
        currentDictionaryVersion = identityNormalizer.CurrentDictionaryVersion;
        dictionarySnapshotVersion = currentDictionaryVersion;
        batchBuilder.ResetRetainingCapacity();
        batchBuilder.EnsureCapacity(initialEventCapacity, initialPayloadCapacity);

        if (!sameRadar)
        {
            if (sourceUniverse.RadarOrdinalCount == 1 && identityNormalizer.RadarCount > 0)
            {
                identityNormalizer = new RadarStreamIdentityNormalizer(sourceUniverse);
                currentDictionaryVersion = DictionaryVersion.Initial;
                dictionarySnapshotVersion = DictionaryVersion.Initial;
            }

            radarIdUtf8 = Encoding.ASCII.GetBytes(radarId);
            identityCacheByMomentCode.Clear();
        }
    }

    /// <summary>
    /// Builds an owned batch from currently staged events and resets the batch builder.
    /// </summary>
    public RadarEventBatch BuildBatch()
    {
        var batch = batchBuilder.BuildAndReset();
        if (batch.DictionaryVersion.Value > dictionarySnapshotVersion.Value)
        {
            dictionarySnapshotVersion = batch.DictionaryVersion;
        }

        return batch;
    }

    /// <summary>
    /// Publishes a leased batch when events are staged and records the published dictionary version.
    /// </summary>
    public void PublishLeasedBatch(
        IArchiveRadarEventBatchPublisher publisher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        if (batchBuilder.EventCount == 0)
        {
            return;
        }

        var publishedDictionaryVersion = DictionaryVersion.Initial;
        batchBuilder.ConsumeLeased(batch =>
        {
            publishedDictionaryVersion = batch.DictionaryVersion;
            publisher.Publish(batch, cancellationToken);
        });

        if (publishedDictionaryVersion.Value > dictionarySnapshotVersion.Value)
        {
            dictionarySnapshotVersion = publishedDictionaryVersion;
        }
    }

    /// <inheritdoc />
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
        return (radialSequence - 1) % azimuthBucketCount;
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
                volumeTimestampUtcTicks,
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
            if (result.Identity.DictionaryVersion.Value > currentDictionaryVersion.Value)
            {
                currentDictionaryVersion = result.Identity.DictionaryVersion;
            }

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
        if ((uint)elevationSlot >= (uint)elevationSlotCount ||
            (uint)azimuthBucket >= (uint)azimuthBucketCount ||
            (uint)rangeBand >= (uint)rangeBandCount)
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
            (elevationSlot * sourcesPerElevationSlot) +
            (azimuthBucket * sourcesPerAzimuthBucket) +
            rangeBand;

        return new RadarStreamIdentity(
            sourceId,
            dimensions.RadarOrdinal,
            dimensions.MomentId,
            checked((ushort)elevationSlot),
            checked((ushort)azimuthBucket),
            checked((ushort)rangeBand),
            currentDictionaryVersion,
            sourceUniverseVersion);
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
