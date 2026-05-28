using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderReadinessGateTests
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
        Assert.Equal(1, (int)RadarProcessingQueuedProviderReadinessError.NotEvaluated);
        Assert.Equal(2, (int)RadarProcessingQueuedProviderReadinessError.MissingBorrowedReference);
        Assert.Equal(3, (int)RadarProcessingQueuedProviderReadinessError.QueuedProviderValidationFailed);
        Assert.Equal(4, (int)RadarProcessingQueuedProviderReadinessError.ChecksumMismatch);
        Assert.Equal(5, (int)RadarProcessingQueuedProviderReadinessError.TopologyOrRebalanceMismatch);
        Assert.Equal(6, (int)RadarProcessingQueuedProviderReadinessError.RetainedResourceReleaseFailed);
        Assert.Equal(7, (int)RadarProcessingQueuedProviderReadinessError.RetainedResourceCleanupIncomplete);
        Assert.Equal(8, (int)RadarProcessingQueuedProviderReadinessError.RetainedResourceRetentionFailed);
        Assert.Equal(9, (int)RadarProcessingQueuedProviderReadinessError.MissingRetainedResourcePressureTelemetry);
        Assert.Equal(10, (int)RadarProcessingQueuedProviderReadinessError.MissingActiveRetainedTelemetry);
        Assert.Equal(11, (int)RadarProcessingQueuedProviderReadinessError.CombinedRetainedPayloadBudgetExceeded);
        Assert.Equal(12, (int)RadarProcessingQueuedProviderReadinessError.ControlledProofExcluded);
        Assert.Equal(13, (int)RadarProcessingQueuedProviderReadinessError.CandidateContourMismatch);
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
    public void RolloutThresholdsCaptureMilestone012GateValues()
    {
        var thresholds = RadarProcessingQueuedProviderRolloutThresholds.Default;

        Assert.Equal(0, RadarProcessingQueuedProviderRolloutThresholds.RequiredReleaseFailureCount);
        Assert.Equal(0, RadarProcessingQueuedProviderRolloutThresholds.RequiredCurrentRetainedBatchCount);
        Assert.Equal(0, RadarProcessingQueuedProviderRolloutThresholds.RequiredCurrentRetainedPayloadBytes);
        Assert.Equal(536_870_912, thresholds.CombinedRetainedPayloadBytesBudget);
        Assert.Equal(1.10, thresholds.MaximumCandidateToBorrowedAllocationRatio);
        Assert.Equal(1.00, thresholds.MaximumCandidateToBorrowedElapsedRatio);
        Assert.Equal(0.075, thresholds.MaximumCandidateRunSpreadRatio);

        var custom = new RadarProcessingQueuedProviderRolloutThresholds(
            combinedRetainedPayloadBytesBudget: 1024,
            maximumCandidateToBorrowedAllocationRatio: 1.05,
            maximumCandidateToBorrowedElapsedRatio: 1.02,
            maximumCandidateRunSpreadRatio: 0.03);

        Assert.Equal(1024, custom.CombinedRetainedPayloadBytesBudget);
        Assert.Equal(1.05, custom.MaximumCandidateToBorrowedAllocationRatio);
        Assert.Equal(1.02, custom.MaximumCandidateToBorrowedElapsedRatio);
        Assert.Equal(0.03, custom.MaximumCandidateRunSpreadRatio);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedProviderRolloutThresholds(combinedRetainedPayloadBytesBudget: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedProviderRolloutThresholds(maximumCandidateToBorrowedAllocationRatio: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedProviderRolloutThresholds(maximumCandidateToBorrowedElapsedRatio: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingQueuedProviderRolloutThresholds(maximumCandidateRunSpreadRatio: -0.01));
    }
}
