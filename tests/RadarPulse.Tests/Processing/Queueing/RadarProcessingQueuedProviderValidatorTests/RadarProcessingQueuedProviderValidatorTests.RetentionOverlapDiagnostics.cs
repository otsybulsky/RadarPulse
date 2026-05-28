using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderValidatorTests
{
    [Fact]
    public void BenchmarkProfileAcceptsOptimizedQueuedTelemetryAndSurfacesDiagnostics()
    {
        var session = CreateValidCompletedSession(checksum: 10);
        var reference = RadarProcessingQueuedProviderReference.FromQueuedSession(session);
        var context = CreateValidationContext(session);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            reference,
            context);

        Assert.True(result.IsValid, result.Message);
        Assert.Equal(RadarProcessingQueuedProviderValidationSurface.ProcessingOnly, result.SemanticSurface);
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.ProducerConsumer, result.OverlapMode);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.RetentionStrategy);
    }

    [Fact]
    public void DiagnosticProfileRejectsMissingRetentionTelemetryForAcceptedRetainedBatch()
    {
        var session = CreateValidCompletedSession(checksum: 10);
        var context = new RadarProcessingQueuedProviderValidationContext(
            overlapMode: RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
            overlapElapsed: TimeSpan.FromMilliseconds(1));

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            context: context);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.RetentionTelemetryIncomplete, result.Error);
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.ProducerConsumer, result.OverlapMode);
    }

    [Fact]
    public void DiagnosticProfileRejectsPendingRetainedResourcesAtCompletion()
    {
        var session = CreateValidCompletedSession(checksum: 10);
        var context = CreateValidationContext(
            session,
            releaseNotRequiredCount: 0);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            context: context);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.RetainedResourceCleanupIncomplete, result.Error);
        Assert.Equal(1, result.ExpectedCount);
        Assert.Equal(0, result.ActualCount);
    }

    [Fact]
    public void DiagnosticProfileRejectsMissingProducerConsumerOverlapTelemetry()
    {
        var session = CreateValidCompletedSession(checksum: 10);
        var context = CreateValidationContext(
            session,
            overlapElapsed: TimeSpan.Zero);

        var result = RadarProcessingQueuedProviderValidator.ValidateSessionResult(
            session,
            context: context);

        Assert.False(result.IsValid);
        Assert.Equal(RadarProcessingQueuedProviderValidationError.OverlapTelemetryIncomplete, result.Error);
    }

}
