using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncCoreSessionTests
{
    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        RadarProcessingExecutionMode mode,
        int partitionCount,
        int shardCount,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                mode,
                partitionCount,
                shardCount,
                asyncExecution: asyncExecution));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);
}
