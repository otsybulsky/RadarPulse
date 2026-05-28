using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests
{
    [Fact]
    public async Task LiveAdapterShapeValidationFailureCleansRetainedPressureWithoutBorrowedFallback()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 4,
            shardCount: 2);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateEightBitBatch(universe.Version, [0, 1], messageTimestampBase: 100),
                CreateInvalidSourceBatch(universe.Version),
                CreateEightBitBatch(universe.Version, [2, 3], messageTimestampBase: 300)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunRebalanceAsync(adapter.PublishTo, session);

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted, result.Status);
        Assert.True(result.IsFaulted);
        Assert.True(result.Producer.IsCompleted);
        Assert.True(result.Consumer.IsFaulted);
        Assert.Equal(3, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);

        Assert.Equal(3, result.Consumer.SessionResult.ProcessingResults.Count);
        Assert.Equal(
            [
                RadarProcessingQueuedBatchProcessingStatus.Succeeded,
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation,
                RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault
            ],
            result.Consumer.SessionResult.ProcessingResults
                .Select(static processing => processing.Status)
                .ToArray());
        Assert.Equal(1, result.QueueTelemetry.CompletedBatchCount);
        Assert.Equal(1, result.QueueTelemetry.FailedBatchCount);
        Assert.Equal(1, result.QueueTelemetry.SkippedAfterFaultCount);
        Assert.Equal(
            RadarProcessingValidationError.SourceIdOutsideUniverse,
            result.Consumer.SessionResult.ProcessingResults[1].ProcessingResult?.Validation.Error);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
    }

    [Fact]
    public async Task OrderedConcurrentProcessingValidationFailureCleansRetainedPressureWithoutBorrowedFallback()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            universe,
            partitionCount: 4,
            shardCount: 2);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateInvalidSourceBatch(universe.Version),
                CreateEightBitBatch(universe.Version, [0, 1], messageTimestampBase: 100)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunProcessingAsync(
            adapter.PublishTo,
            core,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted, result.Status);
        Assert.True(result.IsFaulted);
        Assert.Equal(2, result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.True(result.Producer.IsCompleted);
        Assert.True(result.Consumer.IsFaulted);
        Assert.Equal(2, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(
            [
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation,
                RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault
            ],
            result.Consumer.SessionResult.ProcessingResults
                .Select(static processing => processing.Status)
                .ToArray());
        Assert.Equal(0, core.CreateMetrics().ProcessedBatchCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
    }

    [Fact]
    public async Task OrderedConcurrentRebalanceValidationFailureCleansRetainedPressureWithoutBorrowedFallback()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var session = RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession(
            universe,
            partitionCount: 4,
            shardCount: 2);
        var adapter = new DeterministicArchiveLiveAdapter(
            universe,
            [
                CreateInvalidSourceBatch(universe.Version),
                CreateEightBitBatch(universe.Version, [0, 1], messageTimestampBase: 100)
            ]);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();

        var result = await runner.RunOrderedRebalanceAsync(
            adapter.PublishTo,
            session,
            new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity: 2));

        Assert.Equal(RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted, result.Status);
        Assert.True(result.IsFaulted);
        Assert.Equal(2, result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.True(result.Producer.IsCompleted);
        Assert.True(result.Consumer.IsFaulted);
        Assert.Equal(2, result.ProviderResult.AcceptedPublishCount);
        Assert.Equal(0, result.ProviderResult.RejectedPublishCount);
        Assert.Equal(0, result.ProviderResult.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.QueueTelemetry.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(
            [
                RadarProcessingQueuedBatchProcessingStatus.FailedValidation,
                RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault
            ],
            result.Consumer.SessionResult.ProcessingResults
                .Select(static processing => processing.Status)
                .ToArray());
        Assert.Equal(0, session.Core.CreateMetrics().ProcessedBatchCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
    }

}
