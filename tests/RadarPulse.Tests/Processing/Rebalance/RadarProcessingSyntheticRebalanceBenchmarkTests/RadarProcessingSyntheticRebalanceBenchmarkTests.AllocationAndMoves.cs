using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceBenchmarkTests
{
    [Fact]
    public void BenchmarkResultIncludesAllocationAttributionAndProfiles()
    {
        var hardeningOptions = new RadarProcessingRebalanceHardeningOptions(
            telemetryRetention: new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Counters),
            validationProfile: RadarProcessingValidationProfile.Benchmark);

        var result = new RadarProcessingSyntheticRebalanceBenchmark().Measure(
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            iterations: 1,
            warmupIterations: 0,
            hardeningOptions: hardeningOptions);

        Assert.Equal(RadarProcessingValidationProfile.Benchmark, result.ValidationProfile);
        Assert.Equal(RadarProcessingDiagnosticRetentionMode.Counters, result.RetentionMode);
        Assert.True(result.AllocationSummary.IsMeasured);
        Assert.False(result.AllocationSummary.IncludesArchiveReplayAndBatchConstruction);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
        Assert.Equal(result.AllocatedBytes, result.AllocationSummary.MeasuredAllocatedBytes);
        Assert.Equal(result.AllocatedBytes, result.ProcessingCallbackAllocatedBytes);
        Assert.Equal(result.AllocatedBytesPerPayloadValue, result.ProcessingCallbackAllocatedBytesPerPayloadValue);
    }

    [Theory]
    [InlineData(RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance)]
    [InlineData(RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly)]
    [InlineData(RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession)]
    public void SameRunModesPopulateComparableAllocationFields(
        RadarProcessingSyntheticRebalanceBenchmarkMode mode)
    {
        var result = Measure(
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced,
            mode);

        Assert.True(result.AllocationSummary.IsMeasured);
        Assert.Equal(RadarProcessingValidationProfile.Diagnostic, result.ValidationProfile);
        Assert.Equal(RadarProcessingDiagnosticRetentionMode.Recent, result.RetentionMode);
        Assert.Equal(result.AllocatedBytes, result.AllocationSummary.MeasuredAllocatedBytes);
        Assert.Equal(result.AllocatedBytes, result.AllocationSummary.ProcessingCallbackAllocatedBytes);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    [Fact]
    public void DirectReliefBenchmarkRecordsAcceptedDirectMoves()
    {
        var result = Measure(
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession);

        Assert.True(result.AcceptedMoveCount >= 1);
        Assert.True(result.DirectHotReliefCount >= 1);
        Assert.Equal(0, result.ColdEvacuationCount);
        Assert.NotEmpty(result.AcceptedMovePressures);
        Assert.All(
            result.AcceptedMovePressures,
            pressure => Assert.True(pressure.ExpectedRelief > 0));
        Assert.True(result.ValidationSucceeded);
    }

    [Fact]
    public void ColdEvacuationBenchmarkRecordsAcceptedColdMoves()
    {
        var result = Measure(
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession);

        Assert.Equal(1, result.AcceptedMoveCount);
        Assert.Equal(0, result.DirectHotReliefCount);
        Assert.Equal(1, result.ColdEvacuationCount);
        Assert.Contains(
            result.AcceptedMovePressures,
            pressure => pressure.MoveKind == Domain.Processing.RadarProcessingRebalanceMoveKind.ColdEvacuation);
        Assert.True(result.SkippedReasons.Count > 0);
        Assert.True(result.ValidationSucceeded);
    }
}
