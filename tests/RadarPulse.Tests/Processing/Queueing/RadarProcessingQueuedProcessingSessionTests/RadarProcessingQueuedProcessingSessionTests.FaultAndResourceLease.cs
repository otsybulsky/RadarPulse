using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProcessingSessionTests
{
    [Fact]
    public async Task InvalidQueuedBatchFaultsSessionSkipsAcceptedRemainderAndRejectsLaterEnqueue()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(universe, RadarProcessingExecutionMode.Sequential);
        using var session = new RadarProcessingQueuedProcessingSession(
            core,
            new RadarProcessingProviderQueueOptions(capacity: 2));

        var bad = await session.EnqueueAsync(CreateInvalidSourceBatch(universe.Version));
        var acceptedBeforeFault = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 100, firstPayloadValue: 1));

        var result = await session.DrainAsync();
        var rejectedAfterFault = await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 101, firstPayloadValue: 3));

        Assert.True(bad.IsAccepted);
        Assert.True(acceptedBeforeFault.IsAccepted);
        Assert.True(result.IsFaulted);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Faulted, result.Status);
        Assert.Equal(2, result.ProcessingResults.Count);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, result.ProcessingResults[0].Status);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, result.ProcessingResults[0].ProcessingResult?.Validation.Error);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault, result.ProcessingResults[1].Status);
        Assert.Equal(RadarProcessingQueuedBatchEnqueueStatus.Faulted, rejectedAfterFault.Status);
        Assert.Equal(1, result.Telemetry.FailedBatchCount);
        Assert.Equal(1, result.Telemetry.SkippedAfterFaultCount);
    }

    [Fact]
    public async Task ConsumerResourceLeaseFactoryWrapsDequeuedBatchesIncludingSkippedAfterFault()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var acquired = new List<long>();
        var released = new List<long>();
        using var session = new RadarProcessingQueuedProcessingSession(
            CreateCore(universe, RadarProcessingExecutionMode.Sequential),
            new RadarProcessingProviderQueueOptions(capacity: 2),
            sequence =>
            {
                acquired.Add(sequence.Value);
                return new CallbackDisposable(() => released.Add(sequence.Value));
            });

        await session.EnqueueAsync(CreateInvalidSourceBatch(universe.Version));
        await session.EnqueueAsync(
            CreateBatch(universe.Version, sourceId: 0, messageTimestampUtcTicks: 100, firstPayloadValue: 1));

        var result = await session.DrainAsync();

        Assert.True(result.IsFaulted);
        Assert.Equal(
            [RadarProcessingQueuedBatchProcessingStatus.FailedValidation, RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault],
            result.ProcessingResults.Select(static item => item.Status).ToArray());
        Assert.Equal([0L, 1L], acquired);
        Assert.Equal([0L, 1L], released);
    }

    [Fact]
    public async Task ConsumerValidationFailureReleasesActiveResource()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.Sequential,
            handlers: [new ThrowingHandler()]);
        using var queue = new RadarProcessingOwnedBatchQueue(
            new RadarProcessingProviderQueueOptions(capacity: 1));
        using var publisher = new ArchiveOwnedRadarEventBatchQueueingPublisher(
            queue,
            retainedPayloadOptions: new RadarProcessingRetainedPayloadOptions(
                RadarProcessingRetainedPayloadStrategy.PooledCopy));
        using var session = new RadarProcessingQueuedProcessingSession(
            core,
            queue,
            ownsQueue: false,
            consumerResourceLeaseFactory: publisher.AcquireConsumerResourceLease);

        PublishLeased(publisher, universe.Version, [1, 2]);
        publisher.CompleteAdding();

        var result = await session.DrainAsync();
        var provider = publisher.CreateResult();

        Assert.True(result.IsFaulted);
        var processing = Assert.Single(result.ProcessingResults);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, processing.Status);
        Assert.Contains("handler failed", processing.Message, StringComparison.Ordinal);
        Assert.Equal(0, provider.Telemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, provider.Telemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, provider.Telemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(1, provider.Telemetry.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(2, provider.Telemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, provider.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, provider.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(0, provider.RetentionTelemetry.ReleaseFailedCount);
    }
}
