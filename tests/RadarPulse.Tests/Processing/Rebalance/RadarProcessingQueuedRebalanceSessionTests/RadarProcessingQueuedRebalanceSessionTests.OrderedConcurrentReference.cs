using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSessionTests
{
    [Fact]
    public async Task OrderedConcurrentRebalancePreservesAcceptedMoveAgainstDirectReference()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var ordered = CreateSession(universe);
        var batch = CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]);
        var referenceResult = reference.Process(batch);
        await using var session = new RadarProcessingQueuedRebalanceSession(
            ordered,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        var enqueue = await session.EnqueueAsync(batch);
        session.CompleteAdding();
        var result = await session.DrainOrderedConcurrentAsync(
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.True(enqueue.IsAccepted);
        Assert.True(result.IsCompleted);
        Assert.Equal(reference.CurrentTopology.Version, ordered.CurrentTopology.Version);
        Assert.Equal(reference.CurrentTopology.Version, result.FinalTopologyVersion);
        var processing = Assert.Single(result.ProcessingResults);
        Assert.True(processing.IsSuccessful);
        Assert.True(processing.RebalanceResult?.Validation.IsValid);
        Assert.Equal(referenceResult.PublishedMigration, processing.RebalanceResult?.PublishedMigration);
        Assert.Equal(
            referenceResult.TelemetrySummary.Counters.AcceptedMoveCount,
            processing.RebalanceResult?.TelemetrySummary.Counters.AcceptedMoveCount);
        Assert.Equal(1, result.Telemetry.CompletedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
    }

    [Fact]
    public async Task OrderedConcurrentRebalancePreservesNoActionCountsAgainstDirectReference()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var ordered = CreateSession(universe);
        var batches = new[]
        {
            CreateEmptyBatch(universe.Version),
            CreateEmptyBatch(universe.Version),
            CreateEmptyBatch(universe.Version)
        };
        var referenceResults = batches.Select(batch => reference.Process(batch)).ToArray();
        await using var session = new RadarProcessingQueuedRebalanceSession(
            ordered,
            new RadarProcessingProviderQueueOptions(capacity: 3));

        foreach (var batch in batches)
        {
            var enqueue = await session.EnqueueAsync(batch);
            Assert.True(enqueue.IsAccepted);
        }

        session.CompleteAdding();
        var result = await session.DrainOrderedConcurrentAsync(
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.True(result.IsCompleted);
        Assert.Equal([0L, 1L, 2L], result.ProcessingResults.Select(static processing => processing.Sequence.Value));
        Assert.All(result.ProcessingResults, static processing => Assert.True(processing.IsSuccessful));
        Assert.Equal(reference.CurrentTopology.Version, ordered.CurrentTopology.Version);
        Assert.Equal(
            referenceResults[^1].TelemetrySummary.Counters.NoActionDecisionCount,
            result.ProcessingResults[^1].RebalanceResult?.TelemetrySummary.Counters.NoActionDecisionCount);
        Assert.Equal(3, result.Telemetry.CompletedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
    }

    [Fact]
    public async Task OrderedConcurrentRebalanceRecomputesLaterActiveDeltaAfterTopologyMove()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var reference = CreateSession(universe);
        var ordered = CreateSession(universe);
        var batches = new[]
        {
            CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]),
            CreateEmptyBatch(universe.Version)
        };
        var referenceResults = batches.Select(batch => reference.Process(batch)).ToArray();
        await using var session = new RadarProcessingQueuedRebalanceSession(
            ordered,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        foreach (var batch in batches)
        {
            var enqueue = await session.EnqueueAsync(batch);
            Assert.True(enqueue.IsAccepted);
        }

        session.CompleteAdding();
        var result = await session.DrainOrderedConcurrentAsync(
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.True(result.IsCompleted);
        Assert.Equal(reference.CurrentTopology.Version, ordered.CurrentTopology.Version);
        Assert.Equal(reference.CurrentTopology.Version, result.FinalTopologyVersion);
        Assert.Equal([0L, 1L], result.ProcessingResults.Select(static processing => processing.Sequence.Value));
        Assert.All(result.ProcessingResults, static processing => Assert.True(processing.IsSuccessful));
        Assert.True(result.ProcessingResults[0].RebalanceResult?.PublishedMigration);
        Assert.Equal(
            referenceResults[0].TelemetrySummary.Counters.AcceptedMoveCount,
            result.ProcessingResults[0].RebalanceResult?.TelemetrySummary.Counters.AcceptedMoveCount);

        var secondProcessing = result.ProcessingResults[1].ProcessingResult;
        var secondTelemetry = Assert.IsType<RadarProcessingTelemetry>(secondProcessing?.Telemetry);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), secondProcessing?.TopologyVersion);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), secondTelemetry.TopologyVersion);
        Assert.Equal(referenceResults[1].ProcessingResult.TopologyVersion, secondProcessing?.TopologyVersion);
        Assert.Equal(2, result.Telemetry.CompletedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
    }

}
