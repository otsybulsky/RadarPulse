using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderReadinessGateTests
{
    [Fact]
    public void CorrectnessGateRequiresBorrowedReferenceForDefaultReadiness()
    {
        var validation = RadarProcessingQueuedProviderValidationResult.Valid(
            RadarProcessingQueuedProviderValidationProfile.Benchmark);

        var missingReference = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateCorrectnessParity(
            validation,
            hasBorrowedReference: false);
        var optInDiagnostic = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateCorrectnessParity(
            validation,
            hasBorrowedReference: false,
            requiresBorrowedReference: false);

        Assert.True(missingReference.IsInconclusive);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.MissingBorrowedReference, missingReference.Error);
        Assert.True(optInDiagnostic.IsPassed);
    }

    [Fact]
    public void CorrectnessGatePreservesChecksumMismatchDiagnostics()
    {
        var validation = RadarProcessingQueuedProviderValidationResult.Invalid(
            RadarProcessingQueuedProviderValidationError.DeterministicChecksumMismatch,
            "checksum mismatch",
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            expectedChecksum: 10,
            actualChecksum: 11);

        var readiness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateCorrectnessParity(
            validation,
            hasBorrowedReference: true);

        Assert.True(readiness.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.CorrectnessParity, readiness.Gate);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.ChecksumMismatch, readiness.Error);
        Assert.Equal(10UL, readiness.ExpectedChecksum);
        Assert.Equal(11UL, readiness.ActualChecksum);
        Assert.Equal("checksum mismatch", readiness.Message);
    }

    [Fact]
    public void CorrectnessGateSeparatesTopologyAndRebalanceParityFailures()
    {
        var validation = RadarProcessingQueuedProviderValidationResult.Invalid(
            RadarProcessingQueuedProviderValidationError.AcceptedMoveCountMismatch,
            "accepted move mismatch",
            RadarProcessingQueuedProviderValidationProfile.Benchmark,
            expectedCount: 2,
            actualCount: 1);

        var readiness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateCorrectnessParity(
            validation,
            hasBorrowedReference: true);

        Assert.True(readiness.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.TopologyAndRebalanceParity, readiness.Gate);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.TopologyOrRebalanceMismatch, readiness.Error);
        Assert.Equal(2, readiness.ExpectedCount);
        Assert.Equal(1, readiness.ActualCount);
    }

    [Fact]
    public void CorrectnessGateReportsQueuedProviderValidationFailureWithoutBorrowedFallback()
    {
        var validation = RadarProcessingQueuedProviderValidationResult.Invalid(
            RadarProcessingQueuedProviderValidationError.RetentionTelemetryIncomplete,
            "retention telemetry incomplete",
            RadarProcessingQueuedProviderValidationProfile.Benchmark);

        var readiness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateCorrectnessParity(
            validation,
            hasBorrowedReference: true);

        Assert.True(readiness.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.CorrectnessParity, readiness.Gate);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.QueuedProviderValidationFailed, readiness.Error);
        Assert.Equal("retention telemetry incomplete", readiness.Message);
    }

    [Fact]
    public void ReleaseHealthFailsReleaseFailureEvenWhenCorrectnessCanPass()
    {
        var correctness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateCorrectnessParity(
            RadarProcessingQueuedProviderValidationResult.Valid(
                RadarProcessingQueuedProviderValidationProfile.Benchmark),
            hasBorrowedReference: true);
        var release = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourceReleaseHealth(
            new RadarProcessingRetainedPayloadTelemetrySummary(
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
                retentionAttemptCount: 2,
                retainedBatchCount: 2,
                releaseAttemptCount: 2,
                releasedBatchCount: 1,
                releaseFailedCount: 1));

        Assert.True(correctness.IsPassed);
        Assert.True(release.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.RetainedResourceReleaseFailed, release.Error);
        Assert.Equal(0, release.ExpectedCount);
        Assert.Equal(1, release.ActualCount);
    }
}
