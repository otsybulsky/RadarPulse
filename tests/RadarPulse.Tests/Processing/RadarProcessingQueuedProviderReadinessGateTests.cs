using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingQueuedProviderReadinessGateTests
{
    [Fact]
    public void ReadinessContractsUseStableValuesAndRejectInvalidShapes()
    {
        Assert.Equal(0, (int)RadarProcessingQueuedProviderReadinessStatus.NotEvaluated);
        Assert.Equal(1, (int)RadarProcessingQueuedProviderReadinessStatus.Passed);
        Assert.Equal(2, (int)RadarProcessingQueuedProviderReadinessStatus.Failed);
        Assert.Equal(3, (int)RadarProcessingQueuedProviderReadinessStatus.Inconclusive);

        Assert.Equal(1, (int)RadarProcessingQueuedProviderReadinessGate.CorrectnessParity);
        Assert.Equal(2, (int)RadarProcessingQueuedProviderReadinessGate.TopologyAndRebalanceParity);
        Assert.Equal(3, (int)RadarProcessingQueuedProviderReadinessGate.RetainedResourceReleaseHealth);
        Assert.Equal(4, (int)RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure);
        Assert.Equal(5, (int)RadarProcessingQueuedProviderReadinessGate.AllocationMovement);
        Assert.Equal(6, (int)RadarProcessingQueuedProviderReadinessGate.PerformanceDelta);
        Assert.Equal(7, (int)RadarProcessingQueuedProviderReadinessGate.RunVariance);
        Assert.Equal(9, (int)RadarProcessingQueuedProviderReadinessGate.NaturalEvidence);

        Assert.Equal(0, (int)RadarProcessingQueuedProviderReadinessError.None);
        Assert.Equal(2, (int)RadarProcessingQueuedProviderReadinessError.MissingBorrowedReference);
        Assert.Equal(4, (int)RadarProcessingQueuedProviderReadinessError.ChecksumMismatch);
        Assert.Equal(6, (int)RadarProcessingQueuedProviderReadinessError.RetainedResourceReleaseFailed);
        Assert.Equal(10, (int)RadarProcessingQueuedProviderReadinessError.MissingActiveRetainedTelemetry);
        Assert.Equal(11, (int)RadarProcessingQueuedProviderReadinessError.CombinedRetainedPayloadBudgetExceeded);
        Assert.Equal(12, (int)RadarProcessingQueuedProviderReadinessError.ControlledProofExcluded);
        Assert.Equal(14, (int)RadarProcessingQueuedProviderReadinessError.PerformanceRegression);
        Assert.Equal(15, (int)RadarProcessingQueuedProviderReadinessError.RunVarianceTooHigh);
        Assert.Equal(16, (int)RadarProcessingQueuedProviderReadinessError.AllocationRegression);

        var passed = RadarProcessingQueuedProviderReadinessResult.Passed(
            RadarProcessingQueuedProviderReadinessGate.CorrectnessParity);
        var failed = RadarProcessingQueuedProviderReadinessResult.Failed(
            RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure,
            RadarProcessingQueuedProviderReadinessError.CombinedRetainedPayloadBudgetExceeded,
            "budget exceeded",
            expectedBytes: 8,
            actualBytes: 16);
        var inconclusive = RadarProcessingQueuedProviderReadinessResult.Inconclusive(
            RadarProcessingQueuedProviderReadinessGate.CorrectnessParity,
            RadarProcessingQueuedProviderReadinessError.MissingBorrowedReference,
            "missing reference");
        var notEvaluated = RadarProcessingQueuedProviderReadinessResult.NotEvaluated(
            RadarProcessingQueuedProviderReadinessGate.PerformanceDelta,
            "no reference duration");

        Assert.True(passed.IsPassed);
        Assert.True(failed.IsFailed);
        Assert.Equal(8, failed.ExpectedBytes);
        Assert.Equal(16, failed.ActualBytes);
        Assert.True(inconclusive.IsInconclusive);
        Assert.False(notEvaluated.IsEvaluated);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingQueuedProviderReadinessResult.Passed((RadarProcessingQueuedProviderReadinessGate)255));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingQueuedProviderReadinessResult.Failed(
                RadarProcessingQueuedProviderReadinessGate.CorrectnessParity,
                RadarProcessingQueuedProviderReadinessError.None,
                "invalid"));
        Assert.Throws<ArgumentException>(() =>
            RadarProcessingQueuedProviderReadinessResult.NotEvaluated(
                RadarProcessingQueuedProviderReadinessGate.CorrectnessParity,
                string.Empty));
    }

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

    [Fact]
    public void NaturalEvidenceRejectsControlledDelayAndNonCandidateContours()
    {
        var controlled = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateNaturalEvidence(
            isDefaultCandidateContour: true,
            overlapConsumerDelay: TimeSpan.FromMilliseconds(1));
        var nonCandidate = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateNaturalEvidence(
            isDefaultCandidateContour: false,
            overlapConsumerDelay: TimeSpan.Zero);
        var naturalCandidate = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateNaturalEvidence(
            isDefaultCandidateContour: true,
            overlapConsumerDelay: TimeSpan.Zero);

        Assert.True(controlled.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.NaturalEvidence, controlled.Gate);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.ControlledProofExcluded, controlled.Error);
        Assert.True(nonCandidate.IsInconclusive);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.EffectiveConfiguration, nonCandidate.Gate);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.CandidateContourMismatch, nonCandidate.Error);
        Assert.True(naturalCandidate.IsPassed);
    }

    [Fact]
    public void RetainedPressureGateFailsCombinedPayloadBudget()
    {
        var pressure = new RadarProcessingRetainedResourcePressureSummary(
            activeRetainedBatchCountHighWatermark: 1,
            activeRetainedPayloadBytesHighWatermark: 8192,
            combinedRetainedBatchCountHighWatermark: 1,
            combinedRetainedPayloadBytesHighWatermark: 8192);

        var readiness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourcePressure(
            pressure,
            combinedRetainedPayloadBytesBudget: 4096,
            requiresActiveRetainedTelemetry: true);

        Assert.True(readiness.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.CombinedRetainedPayloadBudgetExceeded, readiness.Error);
        Assert.Equal(4096, readiness.ExpectedBytes);
        Assert.Equal(8192, readiness.ActualBytes);
    }

    [Fact]
    public void RetainedPressureGateTreatsMissingActiveTelemetryAsInconclusive()
    {
        var pressure = new RadarProcessingRetainedResourcePressureSummary(
            pendingRetainedBatchCountHighWatermark: 1,
            pendingRetainedPayloadBytesHighWatermark: 4096,
            combinedRetainedBatchCountHighWatermark: 1,
            combinedRetainedPayloadBytesHighWatermark: 4096);

        var readiness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourcePressure(
            pressure,
            combinedRetainedPayloadBytesBudget: 8192,
            requiresActiveRetainedTelemetry: true);

        Assert.True(readiness.IsInconclusive);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.MissingActiveRetainedTelemetry, readiness.Error);
        Assert.Equal(1, readiness.ExpectedCount);
        Assert.Equal(0, readiness.ActualCount);
    }

    [Fact]
    public void PerformanceGateFailsRegressionIndependentlyFromCorrectness()
    {
        var correctness = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateCorrectnessParity(
            RadarProcessingQueuedProviderValidationResult.Valid(
                RadarProcessingQueuedProviderValidationProfile.Benchmark),
            hasBorrowedReference: true);
        var performance = RadarProcessingQueuedProviderReadinessEvaluator.EvaluatePerformanceDelta(
            candidateElapsed: TimeSpan.FromMilliseconds(120),
            borrowedReferenceElapsed: TimeSpan.FromMilliseconds(100),
            maximumCandidateToReferenceRatio: 1.05);

        Assert.True(correctness.IsPassed);
        Assert.True(performance.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.PerformanceRegression, performance.Error);
        Assert.Equal(1.05, performance.ExpectedRatio);
        Assert.Equal(1.2, performance.ActualRatio);
    }

    [Fact]
    public void AllocationGateHandlesMissingReferenceAndRegression()
    {
        var missingReference = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateAllocationMovement(
            candidateAllocatedBytes: 1024,
            referenceAllocatedBytes: null,
            maximumCandidateToReferenceRatio: 1.0);
        var regression = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateAllocationMovement(
            candidateAllocatedBytes: 2048,
            referenceAllocatedBytes: 1024,
            maximumCandidateToReferenceRatio: 1.0);

        Assert.False(missingReference.IsEvaluated);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.AllocationMovement, missingReference.Gate);
        Assert.True(regression.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.AllocationRegression, regression.Error);
        Assert.Equal(1024, regression.ExpectedBytes);
        Assert.Equal(2048, regression.ActualBytes);
        Assert.Equal(2.0, regression.ActualRatio);
    }

    [Fact]
    public void RunVarianceGateRequiresRepeatedNaturalMeasurements()
    {
        var missingMeasurements = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRunVariance(
            candidateRelativeStandardDeviation: null,
            maximumRelativeStandardDeviation: 0.05);
        var highVariance = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRunVariance(
            candidateRelativeStandardDeviation: 0.12,
            maximumRelativeStandardDeviation: 0.05);

        Assert.False(missingMeasurements.IsEvaluated);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.RunVariance, missingMeasurements.Gate);
        Assert.True(highVariance.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.RunVarianceTooHigh, highVariance.Error);
        Assert.Equal(0.05, highVariance.ExpectedRatio);
        Assert.Equal(0.12, highVariance.ActualRatio);
    }
}
