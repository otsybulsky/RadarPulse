using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderValidatorTests
{
    [Fact]
    public void QueuedProviderValidationContractsUseStableValues()
    {
        Assert.Equal(0, (int)RadarProcessingQueuedProviderValidationProfile.Off);
        Assert.Equal(1, (int)RadarProcessingQueuedProviderValidationProfile.Essential);
        Assert.Equal(2, (int)RadarProcessingQueuedProviderValidationProfile.Diagnostic);
        Assert.Equal(3, (int)RadarProcessingQueuedProviderValidationProfile.Benchmark);

        Assert.Equal(1, (int)RadarProcessingQueuedProviderValidationSurface.ProcessingOnly);
        Assert.Equal(2, (int)RadarProcessingQueuedProviderValidationSurface.Rebalance);
        Assert.Equal(0, (int)RadarProcessingQueuedProviderOverlapMode.None);
        Assert.Equal(1, (int)RadarProcessingQueuedProviderOverlapMode.ProducerConsumer);

        Assert.Equal(0, (int)RadarProcessingQueuedProviderValidationError.None);
        Assert.Equal(1, (int)RadarProcessingQueuedProviderValidationError.NonOwnedQueuedBatch);
        Assert.Equal(2, (int)RadarProcessingQueuedProviderValidationError.ProviderSequenceRegression);
        Assert.Equal(3, (int)RadarProcessingQueuedProviderValidationError.ProcessingSequenceRegression);
        Assert.Equal(4, (int)RadarProcessingQueuedProviderValidationError.MissingCompletionForAcceptedBatch);
        Assert.Equal(5, (int)RadarProcessingQueuedProviderValidationError.TopologyVersionRegression);
        Assert.Equal(9, (int)RadarProcessingQueuedProviderValidationError.DeterministicChecksumMismatch);
        Assert.Equal(14, (int)RadarProcessingQueuedProviderValidationError.ProviderSequenceGap);
        Assert.Equal(15, (int)RadarProcessingQueuedProviderValidationError.ProcessingSequenceGap);
        Assert.Equal(16, (int)RadarProcessingQueuedProviderValidationError.PayloadValueCountMismatch);
        Assert.Equal(17, (int)RadarProcessingQueuedProviderValidationError.FailedMigrationCountMismatch);
        Assert.Equal(18, (int)RadarProcessingQueuedProviderValidationError.ReferenceSemanticSurfaceMismatch);
        Assert.Equal(19, (int)RadarProcessingQueuedProviderValidationError.RetentionTelemetryIncomplete);
        Assert.Equal(20, (int)RadarProcessingQueuedProviderValidationError.RetentionTelemetryMismatch);
        Assert.Equal(21, (int)RadarProcessingQueuedProviderValidationError.RetainedResourceCleanupIncomplete);
        Assert.Equal(22, (int)RadarProcessingQueuedProviderValidationError.OverlapTelemetryIncomplete);

        var context = new RadarProcessingQueuedProviderValidationContext(
            overlapMode: RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
            retentionStrategy: RadarProcessingRetainedPayloadStrategy.PooledCopy,
            retentionTelemetry: new RadarProcessingRetainedPayloadTelemetrySummary(
                RadarProcessingRetainedPayloadStrategy.PooledCopy),
            overlapElapsed: TimeSpan.FromMilliseconds(1));

        var valid = RadarProcessingQueuedProviderValidationResult.Valid(
            RadarProcessingQueuedProviderValidationProfile.Diagnostic,
            context);
        var invalid = RadarProcessingQueuedProviderValidationResult.Invalid(
            RadarProcessingQueuedProviderValidationError.FailureCountMismatch,
            "failed batch count mismatch",
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            expectedCount: 1,
            actualCount: 2,
            context: context);

        Assert.True(valid.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.None, valid.Error);
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.ProducerConsumer, valid.OverlapMode);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, valid.RetentionStrategy);
        Assert.False(invalid.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.FailureCountMismatch, invalid.Error);
        Assert.Equal(1, invalid.ExpectedCount);
        Assert.Equal(2, invalid.ActualCount);
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.ProducerConsumer, invalid.OverlapMode);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingQueuedProviderValidationResult.Valid((RadarProcessingQueuedProviderValidationProfile)255));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingQueuedProviderValidationResult.Invalid(
                RadarProcessingQueuedProviderValidationError.None,
                "invalid",
                RadarProcessingQueuedProviderValidationProfile.Diagnostic));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedProviderReference(failedBatchCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedProviderReference(payloadValueCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedProviderReference(failedMigrationCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedProviderValidationContext(
                semanticSurface: (RadarProcessingQueuedProviderValidationSurface)255));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedProviderValidationContext(
                overlapMode: (RadarProcessingQueuedProviderOverlapMode)255));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingQueuedProviderValidationContext(
                retentionStrategy: RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                retentionTelemetry: new RadarProcessingRetainedPayloadTelemetrySummary(
                    RadarProcessingRetainedPayloadStrategy.PooledCopy)));
    }

}
