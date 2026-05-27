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
        : this(sourceUniverse, degreeOfParallelism: 1)
    {
    }

    public ArchiveRadarEventBatchPublishOptions(
        RadarSourceUniverse sourceUniverse,
        int degreeOfParallelism)
    {
        SourceUniverse = sourceUniverse ?? throw new ArgumentNullException(nameof(sourceUniverse));
        if (degreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degreeOfParallelism), "Degree of parallelism must be greater than zero.");
        }

        DegreeOfParallelism = degreeOfParallelism;
    }

    public RadarSourceUniverse SourceUniverse { get; }

    public int DegreeOfParallelism { get; }
}
