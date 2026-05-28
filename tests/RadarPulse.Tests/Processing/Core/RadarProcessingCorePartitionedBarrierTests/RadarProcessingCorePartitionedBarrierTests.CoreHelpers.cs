using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingCorePartitionedBarrierTests
{
    private static RadarProcessingCore CreatePartitionedCore(
        RadarSourceUniverse universe,
        int partitionCount,
        int shardCount) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount,
                shardCount));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);
}
