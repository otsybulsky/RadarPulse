using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProcessingSessionTests
{
    [Fact]
    public async Task DrainCancellationBeforeDequeueReturnsCanceledSessionResult()
    {
        var universe = CreateUniverse(sourceCount: 1);
        using var session = new RadarProcessingQueuedProcessingSession(
            CreateCore(universe, RadarProcessingExecutionMode.Sequential));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await session.DrainAsync(cancellation.Token);

        Assert.True(result.IsCanceled);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Canceled, result.Status);
        Assert.Empty(result.ProcessingResults);
        Assert.Equal(0, result.Telemetry.CompletedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
    }

    [Fact]
    public async Task CancelQueuedShutdownCancelsAcceptedPendingBatchesWithoutProcessing()
    {
        var universe = CreateUniverse(sourceCount: 1);
        using var session = new RadarProcessingQueuedProcessingSession(
            CreateCore(universe, RadarProcessingExecutionMode.Sequential),
            new RadarProcessingProviderQueueOptions(
                capacity: 2,
                shutdownMode: RadarProcessingProviderQueueShutdownMode.CancelQueued));
        var first = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 100, firstPayloadValue: 1));
        var second = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 101, firstPayloadValue: 3));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await session.DrainAsync(cancellation.Token);

        Assert.True(first.IsAccepted);
        Assert.True(second.IsAccepted);
        Assert.True(result.IsCanceled);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Canceled, result.Status);
        Assert.Equal(0, session.Queue.PendingCount);
        Assert.Equal(0, session.Queue.PendingPayloadBytes);
        Assert.Equal(
            [RadarProcessingQueuedBatchProcessingStatus.Canceled, RadarProcessingQueuedBatchProcessingStatus.Canceled],
            result.ProcessingResults.Select(static item => item.Status).ToArray());
        Assert.Equal([0L, 1L], result.ProcessingResults.Select(static item => item.Sequence.Value).ToArray());
        Assert.Equal(2, result.Telemetry.EnqueuedBatchCount);
        Assert.Equal(0, result.Telemetry.DequeuedBatchCount);
        Assert.Equal(0, result.Telemetry.CompletedBatchCount);
        Assert.Equal(2, result.Telemetry.CanceledBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.Telemetry.CurrentPendingRetainedPayloadBytes);
    }

    [Fact]
    public async Task QueuedProcessingSessionRejectsInvalidAsyncComposition()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var sequential = CreateCore(universe, RadarProcessingExecutionMode.Sequential);
        var asyncCore = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            partitionCount: 1,
            shardCount: 1);
        var asyncSession = new RadarProcessingAsyncCoreSession(asyncCore);

        try
        {
            Assert.Throws<ArgumentException>(() =>
                new RadarProcessingQueuedProcessingSession(
                    sequential,
                    new RadarProcessingOwnedBatchQueue(),
                    asyncSession));
            Assert.Throws<ArgumentNullException>(() =>
                new RadarProcessingQueuedProcessingSession(
                    asyncCore,
                    new RadarProcessingOwnedBatchQueue(),
                    asyncCoreSession: null));
        }
        finally
        {
            await asyncSession.DisposeAsync();
        }
    }
}
