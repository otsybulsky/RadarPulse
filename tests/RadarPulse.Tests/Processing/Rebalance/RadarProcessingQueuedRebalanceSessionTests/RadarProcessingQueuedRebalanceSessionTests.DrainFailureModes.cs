using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSessionTests
{
    [Fact]
    public async Task InvalidQueuedBatchFaultsRebalanceSessionSkipsAcceptedRemainderAndRejectsLaterEnqueue()
    {
        var universe = CreateUniverse(sourceCount: 1);
        using var session = new RadarProcessingQueuedRebalanceSession(
            CreateSession(universe, shardCount: 1),
            new RadarProcessingProviderQueueOptions(capacity: 2));

        var bad = await session.EnqueueAsync(CreateInvalidSourceBatch(universe.Version));
        var acceptedBeforeFault = await session.EnqueueAsync(
            CreateEightBitBatch(universe.Version, [0]));

        var result = await session.DrainAsync();
        var rejectedAfterFault = await session.EnqueueAsync(
            CreateEightBitBatch(universe.Version, [0]));

        Assert.True(bad.IsAccepted);
        Assert.True(acceptedBeforeFault.IsAccepted);
        Assert.True(result.IsFaulted);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Faulted, result.Status);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, result.FinalTopologyVersion);
        Assert.Equal(2, result.ProcessingResults.Count);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, result.ProcessingResults[0].Status);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, result.ProcessingResults[0].ProcessingResult?.Validation.Error);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault, result.ProcessingResults[1].Status);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Faulted, rejectedAfterFault.Status);
        Assert.Equal(1, result.Telemetry.FailedBatchCount);
        Assert.Equal(1, result.Telemetry.SkippedAfterFaultCount);
    }

    [Fact]
    public async Task DrainCancellationBeforeDequeueReturnsCanceledSessionResult()
    {
        var universe = CreateUniverse(sourceCount: 1);
        using var session = new RadarProcessingQueuedRebalanceSession(
            CreateSession(universe, shardCount: 1));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await session.DrainAsync(cancellation.Token);

        Assert.True(result.IsCanceled);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Canceled, result.Status);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, result.FinalTopologyVersion);
        Assert.Empty(result.ProcessingResults);
        Assert.Equal(0, result.Telemetry.CompletedBatchCount);
        Assert.Equal(0, result.Telemetry.FailedBatchCount);
    }

    [Fact]
    public async Task CancelQueuedShutdownCancelsAcceptedPendingBatchesWithoutRebalance()
    {
        var universe = CreateUniverse(sourceCount: 1);
        using var session = new RadarProcessingQueuedRebalanceSession(
            CreateSession(universe, shardCount: 1),
            new RadarProcessingProviderQueueOptions(
                capacity: 2,
                shutdownMode: RadarProcessingProviderQueueShutdownMode.CancelQueued));
        var first = await session.EnqueueAsync(CreateEightBitBatch(universe.Version, [0]));
        var second = await session.EnqueueAsync(CreateEightBitBatch(universe.Version, [0]));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await session.DrainAsync(cancellation.Token);

        Assert.True(first.IsAccepted);
        Assert.True(second.IsAccepted);
        Assert.True(result.IsCanceled);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Canceled, result.Status);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, result.FinalTopologyVersion);
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

}
