using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSessionTests
{
    [Fact]
    public async Task QueuedSyncRebalancePreservesAcceptedMoveCountsAgainstDirectReference()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var queued = CreateSession(universe);
        var batches = new[]
        {
            CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]),
            CreateEmptyBatch(universe.Version)
        };
        var referenceResults = batches.Select(batch => reference.Process(batch)).ToArray();
        using var session = new RadarProcessingQueuedRebalanceSession(
            queued,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        foreach (var batch in batches)
        {
            var enqueue = await session.EnqueueAsync(batch);
            Assert.True(enqueue.IsAccepted);
        }

        session.CompleteAdding();
        var result = await session.DrainAsync();

        Assert.True(result.IsCompleted);
        Assert.Equal(reference.CurrentTopology.Version, queued.CurrentTopology.Version);
        Assert.Equal(reference.CurrentTopology.Version, result.FinalTopologyVersion);
        Assert.Equal([0L, 1L], result.ProcessingResults.Select(static processing => processing.Sequence.Value));
        Assert.All(result.ProcessingResults, static processing => Assert.True(processing.IsSuccessful));
        Assert.Equal(
            referenceResults.Count(static rebalance => rebalance.PublishedMigration),
            result.ProcessingResults.Count(static processing => processing.RebalanceResult?.PublishedMigration == true));
        Assert.Equal(
            referenceResults[^1].TelemetrySummary.Counters.AcceptedMoveCount,
            result.ProcessingResults[^1].RebalanceResult?.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(
            referenceResults[^1].ProcessingResult.TopologyVersion,
            result.ProcessingResults[^1].RebalanceResult?.ProcessingResult.TopologyVersion);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), result.ProcessingResults[^1].TopologyVersion);
        Assert.Equal(2, result.Telemetry.CompletedBatchCount);
        Assert.Equal(2, result.Telemetry.DequeuedBatchCount);
    }

    [Fact]
    public async Task QueuedSyncRebalancePreservesNoActionCountsAgainstDirectReference()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var queued = CreateSession(universe);
        var batches = new[]
        {
            CreateEmptyBatch(universe.Version),
            CreateEmptyBatch(universe.Version)
        };
        var referenceResults = batches.Select(batch => reference.Process(batch)).ToArray();
        using var session = new RadarProcessingQueuedRebalanceSession(
            queued,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        foreach (var batch in batches)
        {
            var enqueue = await session.EnqueueAsync(batch);
            Assert.True(enqueue.IsAccepted);
        }

        session.CompleteAdding();
        var result = await session.DrainAsync();

        Assert.True(result.IsCompleted);
        Assert.Equal(reference.CurrentTopology.Version, result.FinalTopologyVersion);
        Assert.Equal(reference.CurrentTopology.Version, queued.CurrentTopology.Version);
        Assert.All(result.ProcessingResults, static processing => Assert.True(processing.IsSuccessful));
        Assert.Equal(
            referenceResults[^1].TelemetrySummary.Counters.NoActionDecisionCount,
            result.ProcessingResults[^1].RebalanceResult?.TelemetrySummary.Counters.NoActionDecisionCount);
        Assert.Equal(
            referenceResults[^1].TelemetrySummary.Counters.AcceptedMoveCount,
            result.ProcessingResults[^1].RebalanceResult?.TelemetrySummary.Counters.AcceptedMoveCount);
    }

    [Fact]
    public async Task QueuedAsyncRebalanceValidatesThroughAsyncWrapper()
    {
        var universe = CreateUniverse(sourceCount: 4);
        await using var session = new RadarProcessingQueuedRebalanceSession(
            CreateSession(
                universe,
                RadarProcessingExecutionMode.AsyncShardTransport,
                asyncExecution: new RadarProcessingAsyncExecutionOptions(workerCount: 2, queueCapacity: 1)),
            new RadarProcessingProviderQueueOptions(capacity: 1));

        var enqueue = await session.EnqueueAsync(
            CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]));
        session.CompleteAdding();
        var result = await session.DrainAsync();

        Assert.True(enqueue.IsAccepted);
        Assert.True(result.IsCompleted);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), result.FinalTopologyVersion);
        var processing = Assert.Single(result.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.Equal(RadarProcessingExecutionMode.AsyncShardTransport, processing.ProcessingResult?.ExecutionMode);
        Assert.NotNull(processing.RebalanceResult?.WorkerTelemetry);
        Assert.True(processing.RebalanceResult.Validation.IsValid);
        Assert.True(processing.RebalanceResult.PublishedMigration);
        Assert.Equal(1, processing.RebalanceResult.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(1, result.Telemetry.CompletedBatchCount);
    }

}
