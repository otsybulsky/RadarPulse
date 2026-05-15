using RadarPulse.Domain.Streaming;

namespace RadarPulse.Application.Archive;

public sealed record ArchiveRadarEventBatchPublishOptions
{
    public static ArchiveRadarEventBatchPublishOptions DefaultSingleRadar { get; } =
        new(new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 32,
            azimuthBucketCount: 720,
            rangeBandCount: 1));

    public ArchiveRadarEventBatchPublishOptions(RadarSourceUniverse sourceUniverse)
    {
        SourceUniverse = sourceUniverse ?? throw new ArgumentNullException(nameof(sourceUniverse));
    }

    public RadarSourceUniverse SourceUniverse { get; }
}
