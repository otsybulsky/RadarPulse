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
    public async Task CancellationAfterAcceptedEnqueueReleasesPendingResource()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(capacity: 2, recentDetailCapacity: 16),
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));
        using var cancellation = new CancellationTokenSource();

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                PublishLeased(publisher, [1, 2], cancellationToken);
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return CreatePublishResult(batchCount: 1);
            },
            async (queue, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                return CreateSessionResult(
                    queue,
                    Array.Empty<RadarProcessingQueuedBatchProcessingResult>());
            },
            options,
            cancellation.Token);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.Canceled, result.Status);
        Assert.True(result.IsCanceled);
        Assert.True(result.Producer.IsCanceled);
        Assert.True(result.Consumer.IsCanceled);
        Assert.Equal(1, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(1, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
    }

    [Fact]
    public async Task CancelQueuedCancellationClearsQueueAndReleasesPendingResource()
    {
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var options = new RadarProcessingArchiveQueuedOverlapOptions(
            new RadarProcessingProviderQueueOptions(
                capacity: 2,
                recentDetailCapacity: 16,
                shutdownMode: RadarProcessingProviderQueueShutdownMode.CancelQueued),
            new RadarProcessingRetainedPayloadOptions(RadarProcessingRetainedPayloadStrategy.PooledCopy));
        using var cancellation = new CancellationTokenSource();

        var result = await runner.RunAsync(
            (publisher, cancellationToken) =>
            {
                PublishLeased(publisher, [1, 2], cancellationToken);
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return CreatePublishResult(batchCount: 1);
            },
            async (queue, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                return CreateSessionResult(
                    queue,
                    Array.Empty<RadarProcessingQueuedBatchProcessingResult>());
            },
            options,
            cancellation.Token);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.Canceled, result.Status);
        Assert.True(result.IsCanceled);
        Assert.Equal(1, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(1, result.QueueTelemetry.EnqueuedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.DequeuedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(1, result.QueueTelemetry.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleaseAttemptCount);
        Assert.Equal(1, result.ProviderResult.RetentionTelemetry.ReleasedBatchCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
    }

}
