using RadarPulse.Domain.Streaming;

namespace RadarPulse.Application.Archive;

/// <summary>
/// Options for projecting Archive II data into streaming radar event batches.
/// </summary>
public sealed record ArchiveRadarEventBatchPublishOptions
{
    /// <summary>
    /// Default source universe for a single-radar local archive projection.
    /// </summary>
    public static ArchiveRadarEventBatchPublishOptions DefaultSingleRadar { get; } =
        new(new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 32,
            azimuthBucketCount: 720,
            rangeBandCount: 1));

    /// <summary>
    /// Creates single-worker batch publishing options for the supplied source universe.
    /// </summary>
    public ArchiveRadarEventBatchPublishOptions(RadarSourceUniverse sourceUniverse)
        : this(sourceUniverse, degreeOfParallelism: 1)
    {
    }

    /// <summary>
    /// Creates batch publishing options for a source universe and parallel record processing degree.
    /// </summary>
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

    /// <summary>
    /// Gets the source universe used to map projected radar dimensions into compact source ids.
    /// </summary>
    public RadarSourceUniverse SourceUniverse { get; }

    /// <summary>
    /// Gets the number of compressed records that can be decompressed in parallel.
    /// </summary>
    public int DegreeOfParallelism { get; }
}
