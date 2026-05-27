using RadarPulse.Domain.Streaming;

namespace RadarPulse.Application.Processing;

/// <summary>
/// BFF-facing source identity projected from the dense source catalog.
/// </summary>
public sealed class RadarProcessingSourceIdentityReadModel
{
    /// <summary>
    /// Creates source identity read model from source id and source key.
    /// </summary>
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

    /// <summary>
    /// Dense source id.
    /// </summary>
    public int SourceId { get; }

    /// <summary>
    /// Radar ordinal component.
    /// </summary>
    public int RadarOrdinal { get; }

    /// <summary>
    /// Elevation slot component.
    /// </summary>
    public int ElevationSlot { get; }

    /// <summary>
    /// Azimuth bucket component.
    /// </summary>
    public int AzimuthBucket { get; }

    /// <summary>
    /// Range band component.
    /// </summary>
    public int RangeBand { get; }
}
