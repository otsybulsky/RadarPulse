using RadarPulse.Domain.Streaming;

namespace RadarPulse.Application.Processing;

public sealed class RadarProcessingSourceIdentityReadModel
{
    public RadarProcessingSourceIdentityReadModel(
        int sourceId,
        RadarSourceKey key)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceId);

        SourceId = sourceId;
        RadarOrdinal = key.RadarOrdinal;
        ElevationSlot = key.ElevationSlot;
        AzimuthBucket = key.AzimuthBucket;
        RangeBand = key.RangeBand;
    }

    public int SourceId { get; }

    public int RadarOrdinal { get; }

    public int ElevationSlot { get; }

    public int AzimuthBucket { get; }

    public int RangeBand { get; }
}

