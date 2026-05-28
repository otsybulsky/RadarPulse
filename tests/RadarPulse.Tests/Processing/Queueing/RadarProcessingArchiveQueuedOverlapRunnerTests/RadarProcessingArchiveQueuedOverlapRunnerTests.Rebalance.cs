using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingArchiveQueuedOverlapRunnerTests
{
    [Fact]
    public async Task RebalanceOverlapCapturesLatestTopologyWhenQueuedBatchWaitsBehindMigration()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var rebalanceSession = CreateRebalanceSession(universe);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 4, recentDetailCapacity: 16));

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(
                    CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]),
                    cancellationToken);
                publisher.Publish(
                    CreateEmptyBatch(universe.Version),
                    cancellationToken);
                return CreatePublishResult(batchCount: 2);
            },
            async (queue, cancellationToken) =>
            {
                await Task.Delay(100, cancellationToken);
                await using var queuedSession = new RadarProcessingQueuedRebalanceSession(
                    rebalanceSession,
                    queue);
                return await queuedSession.DrainAsync(cancellationToken);
            },
            options);

        Assert.True(result.IsCompleted);
        Assert.Equal(2, result.ProviderResult.AcceptedPublishCount);
        Assert.True(result.QueueTelemetry.QueueDepthHighWatermark > 1);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), result.Consumer.SessionResult.FinalTopologyVersion);

        var first = result.Consumer.SessionResult.ProcessingResults[0];
        var second = result.Consumer.SessionResult.ProcessingResults[1];

        Assert.True(first.RebalanceResult!.PublishedMigration);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, first.TopologyVersion);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), second.TopologyVersion);
        var secondTelemetry = Assert.IsType<RadarProcessingTelemetry>(second.ProcessingResult!.Telemetry);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), secondTelemetry.TopologyVersion);
        Assert.Equal(1, secondTelemetry.Partitions[0].ShardId);
    }

    [Fact]
    public async Task RunRebalanceAsyncUsesOrderedConsumerAndReportsFinalTopology()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var rebalanceSession = CreateRebalanceSession(universe);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16));

        var result = await runner.RunRebalanceAsync(
            (publisher, cancellationToken) =>
            {
                publisher.Publish(
                    CreateEightBitBatch(universe.Version, [0, 0, 0, 0, 1, 1]),
                    cancellationToken);
                publisher.Publish(
                    CreateEmptyBatch(universe.Version),
                    cancellationToken);
                return CreatePublishResult(batchCount: 2);
            },
            rebalanceSession,
            options);

        Assert.True(result.IsCompleted);
        Assert.True(result.Consumer.SessionResult.IsCompleted);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), result.Consumer.SessionResult.FinalTopologyVersion);
        Assert.Equal(RadarProcessingTopologyVersion.Initial.Next(), rebalanceSession.CurrentTopology.Version);
        Assert.Equal(
            [RadarProcessingTopologyVersion.Initial, RadarProcessingTopologyVersion.Initial.Next()],
            result.Consumer.SessionResult.ProcessingResults
                .Select(static processing => processing.TopologyVersion)
                .ToArray());
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.InRange(result.QueueTelemetry.PendingRetainedBatchCountHighWatermark, 1, 2);
        Assert.Equal(6, result.QueueTelemetry.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, result.QueueTelemetry.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(6, result.QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.True(result.QueueTelemetry.CombinedRetainedBatchCountHighWatermark >= result.QueueTelemetry.ActiveRetainedBatchCountHighWatermark);
        Assert.True(result.QueueTelemetry.CombinedRetainedBatchCountHighWatermark >= result.QueueTelemetry.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(6, result.QueueTelemetry.CombinedRetainedPayloadBytesHighWatermark);
        Assert.Same(result.QueueTelemetry.RetainedResourcePressure, result.RetainedResourcePressure);
        Assert.Same(result.QueueTelemetry.RetainedResourcePressure, result.OverlapTelemetry.RetainedResourcePressure);
        Assert.Equal(result.QueueTelemetry.PendingRetainedPayloadBytesHighWatermark, result.OverlapTelemetry.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(result.QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark, result.OverlapTelemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(result.QueueTelemetry.CombinedRetainedPayloadBytesHighWatermark, result.OverlapTelemetry.CombinedRetainedPayloadBytesHighWatermark);
    }

}
