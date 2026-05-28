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
internal sealed partial class ArchiveTwoRadarEventBatchProjector : IArchiveTwoMessageConsumer
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
}
