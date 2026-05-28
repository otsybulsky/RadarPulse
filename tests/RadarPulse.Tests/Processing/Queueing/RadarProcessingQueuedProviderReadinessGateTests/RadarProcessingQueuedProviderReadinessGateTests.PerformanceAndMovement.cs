using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderReadinessGateTests
{
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

    [Fact]
    public void RolloutThresholdsApplyAllocationPerformanceAndRunSpreadRatios()
    {
        var thresholds = RadarProcessingQueuedProviderRolloutThresholds.Default;

        var allocationPass = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateAllocationMovement(
            candidateAllocatedBytes: 1100,
            referenceAllocatedBytes: 1000,
            thresholds.MaximumCandidateToBorrowedAllocationRatio);
        var allocationFail = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateAllocationMovement(
            candidateAllocatedBytes: 1101,
            referenceAllocatedBytes: 1000,
            thresholds.MaximumCandidateToBorrowedAllocationRatio);
        var performancePass = RadarProcessingQueuedProviderReadinessEvaluator.EvaluatePerformanceDelta(
            candidateElapsed: TimeSpan.FromMilliseconds(1000),
            borrowedReferenceElapsed: TimeSpan.FromMilliseconds(1000),
            thresholds.MaximumCandidateToBorrowedElapsedRatio);
        var performanceFail = RadarProcessingQueuedProviderReadinessEvaluator.EvaluatePerformanceDelta(
            candidateElapsed: TimeSpan.FromMilliseconds(1001),
            borrowedReferenceElapsed: TimeSpan.FromMilliseconds(1000),
            thresholds.MaximumCandidateToBorrowedElapsedRatio);
        var spreadMissing = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRunSpread(
            candidateRunSpread: null,
            candidateAverageElapsed: null,
            thresholds.MaximumCandidateRunSpreadRatio);
        var spreadPass = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRunSpread(
            candidateRunSpread: TimeSpan.FromMilliseconds(750),
            candidateAverageElapsed: TimeSpan.FromMilliseconds(10_000),
            thresholds.MaximumCandidateRunSpreadRatio);
        var spreadFail = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRunSpread(
            candidateRunSpread: TimeSpan.FromMilliseconds(751),
            candidateAverageElapsed: TimeSpan.FromMilliseconds(10_000),
            thresholds.MaximumCandidateRunSpreadRatio);

        Assert.True(allocationPass.IsPassed);
        Assert.True(allocationFail.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.AllocationRegression, allocationFail.Error);
        Assert.Equal(1.10, allocationFail.ExpectedRatio);
        Assert.Equal(1.101, allocationFail.ActualRatio);
        Assert.True(performancePass.IsPassed);
        Assert.True(performanceFail.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.PerformanceRegression, performanceFail.Error);
        Assert.True(spreadMissing.Status == RadarProcessingQueuedProviderReadinessStatus.NotEvaluated);
        Assert.True(spreadPass.IsPassed);
        Assert.True(spreadFail.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.RunVarianceTooHigh, spreadFail.Error);
        Assert.Equal(0.075, spreadFail.ExpectedRatio);
        Assert.Equal(0.0751, spreadFail.ActualRatio);
    }
}
