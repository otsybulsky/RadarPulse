using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingQueuedProviderReadinessGateTests
{
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
    public void RetainedCleanupCompletionRequiresCurrentPressureToReturnToZero()
    {
        var missing = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourceCleanupCompletion(
            null);
        var completed = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourceCleanupCompletion(
            new RadarProcessingRetainedResourcePressureSummary(
                activeRetainedBatchCountHighWatermark: 1,
                activeRetainedPayloadBytesHighWatermark: 4096,
                combinedRetainedBatchCountHighWatermark: 1,
                combinedRetainedPayloadBytesHighWatermark: 4096));
        var incomplete = RadarProcessingQueuedProviderReadinessEvaluator.EvaluateRetainedResourceCleanupCompletion(
            new RadarProcessingRetainedResourcePressureSummary(
                currentPendingRetainedBatchCount: 1,
                currentPendingRetainedPayloadBytes: 4096,
                pendingRetainedBatchCountHighWatermark: 1,
                pendingRetainedPayloadBytesHighWatermark: 4096,
                combinedRetainedBatchCountHighWatermark: 1,
                combinedRetainedPayloadBytesHighWatermark: 4096));

        Assert.True(missing.IsInconclusive);
        Assert.Equal(
            RadarProcessingQueuedProviderReadinessError.MissingRetainedResourcePressureTelemetry,
            missing.Error);
        Assert.True(completed.IsPassed);
        Assert.True(incomplete.IsFailed);
        Assert.Equal(RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure, incomplete.Gate);
        Assert.Equal(RadarProcessingQueuedProviderReadinessError.RetainedResourceCleanupIncomplete, incomplete.Error);
        Assert.Equal(0, incomplete.ExpectedCount);
        Assert.Equal(1, incomplete.ActualCount);
        Assert.Equal(0, incomplete.ExpectedBytes);
        Assert.Equal(4096, incomplete.ActualBytes);
    }
}
