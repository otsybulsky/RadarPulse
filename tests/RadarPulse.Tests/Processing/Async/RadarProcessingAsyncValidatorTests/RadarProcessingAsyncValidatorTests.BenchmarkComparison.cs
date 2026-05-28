using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncValidatorTests
{
    [Fact]
    public async Task BenchmarkProfileComparesSyncAndAsyncChecksums()
    {
        var universe = CreateUniverse(sourceCount: 6);
        var syncCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.PartitionedBarrier,
            partitionCount: 6,
            shardCount: 3);
        var asyncCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 6,
            shardCount: 3,
            asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 3, queueCapacity: 1));
        var batch = CreateMixedBatch(universe.Version);

        var syncResult = syncCore.Process(batch);
        await using var asyncSession = new RadarProcessingAsyncCoreSession(asyncCore);
        var asyncResult = await asyncSession.ProcessAsync(batch);

        var result = RadarProcessingAsyncValidator.ValidateBenchmarkComparison(
            syncResult,
            asyncResult,
            syncCore.CreateSourceSnapshots(),
            asyncCore.CreateSourceSnapshots());

        Assert.True(result.IsValid);
        Assert.True(result.HasComparisonChecksums);
        Assert.Equal(syncResult.Metrics.ProcessingChecksum, result.SynchronousChecksum);
        Assert.Equal(asyncResult.Metrics.ProcessingChecksum, result.AsyncChecksum);
    }
}
