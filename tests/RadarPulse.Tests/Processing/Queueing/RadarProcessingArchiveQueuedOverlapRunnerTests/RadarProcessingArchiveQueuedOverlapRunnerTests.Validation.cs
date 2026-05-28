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
    public async Task ValidationFailureReleasesActiveResourceAndFaultsWithoutFallback()
    {
        var universe = CreateUniverse(sourceCount: 1);
        var rebalanceSession = CreateRebalanceSession(universe);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16),
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));

        var result = await runner.RunRebalanceAsync(
            (publisher, cancellationToken) =>
            {
                PublishLeased(publisher, [1, 2], cancellationToken, sourceId: 1);
                return CreatePublishResult(batchCount: 1);
            },
            rebalanceSession,
            options);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted, result.Status);
        Assert.True(result.IsFaulted);
        Assert.True(result.Consumer.IsFaulted);
        Assert.True(result.Producer.IsCompleted);
        Assert.Equal(RadarProcessingQueuedSessionStatus.Faulted, result.Consumer.SessionResult.Status);
        Assert.Equal(result.Consumer.SessionResult.Message, result.Message);
        Assert.Equal(1, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(1, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(1, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(1, result.QueueTelemetry.FailedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CompletedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(1, result.QueueTelemetry.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(2, result.QueueTelemetry.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);

        var processing = Assert.Single(result.Consumer.SessionResult.ProcessingResults);
        Assert.Equal(RadarProcessingQueuedBatchProcessingStatus.FailedValidation, processing.Status);
        Assert.Equal(RadarProcessingValidationError.SourceIdOutsideUniverse, processing.ProcessingResult?.Validation.Error);
    }

}
