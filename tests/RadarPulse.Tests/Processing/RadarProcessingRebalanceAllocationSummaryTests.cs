using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRebalanceAllocationSummaryTests
{
    [Fact]
    public void ProcessingOnlySummaryUsesMeasuredBytesAsCallbackBytes()
    {
        var summary = RadarProcessingRebalanceAllocationSummary.ForProcessingOnly(1_200);

        Assert.True(summary.IsMeasured);
        Assert.Equal(1_200, summary.MeasuredAllocatedBytes);
        Assert.Equal(1_200, summary.ProcessingCallbackAllocatedBytes);
        Assert.Equal(0, summary.ReplayAndBatchConstructionAllocatedBytes);
        Assert.False(summary.IncludesArchiveReplayAndBatchConstruction);
        Assert.False(summary.IncludesCliFormatting);
        Assert.Equal(12.0, summary.MeasuredAllocatedBytesPerPayloadValue(100));
        Assert.Equal(12.0, summary.ProcessingCallbackAllocatedBytesPerPayloadValue(100));
    }

    [Fact]
    public void ArchiveSummarySeparatesCallbackAndReplayAllocation()
    {
        var summary = RadarProcessingRebalanceAllocationSummary.ForArchiveReplay(
            measuredAllocatedBytes: 2_000,
            processingCallbackAllocatedBytes: 750);

        Assert.True(summary.IsMeasured);
        Assert.True(summary.IncludesArchiveReplayAndBatchConstruction);
        Assert.True(summary.HasSeparateProcessingCallbackAllocation);
        Assert.False(summary.IncludesCliFormatting);
        Assert.Equal(2_000, summary.MeasuredAllocatedBytes);
        Assert.Equal(750, summary.ProcessingCallbackAllocatedBytes);
        Assert.Equal(1_250, summary.ReplayAndBatchConstructionAllocatedBytes);
        Assert.Equal(7.5, summary.ProcessingCallbackAllocatedBytesPerPayloadValue(100));
        Assert.Equal(12.5, summary.ReplayAndBatchConstructionAllocatedBytesPerPayloadValue(100));
    }

    [Fact]
    public void ArchiveSummaryDoesNotReportNegativeReplayAllocation()
    {
        var summary = RadarProcessingRebalanceAllocationSummary.ForArchiveReplay(
            measuredAllocatedBytes: 500,
            processingCallbackAllocatedBytes: 750);

        Assert.Equal(0, summary.ReplayAndBatchConstructionAllocatedBytes);
    }

    [Fact]
    public void SnapshotComputesNonNegativeDeltas()
    {
        var before = new RadarProcessingBenchmarkAllocationSnapshot(1_000);
        var after = new RadarProcessingBenchmarkAllocationSnapshot(1_500);

        Assert.Equal(500, after.DeltaSince(before));
        Assert.Equal(0, before.DeltaSince(after));
    }

    [Fact]
    public void ArchiveBenchmarkResultKeepsEndToEndAndCallbackAllocationSeparate()
    {
        var result = new RadarProcessingArchiveRebalanceBenchmarkResult(
            FilePath: "sample",
            Decompressor: "radarpulse",
            Mode: RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession,
            Iterations: 1,
            WarmupIterations: 0,
            DegreeOfParallelism: 1,
            SourceCount: 4,
            PartitionCount: 4,
            ShardCount: 2,
            FileSizeBytesPerIteration: 100,
            CompressedRecordsPerIteration: 1,
            CompressedBytesPerIteration: 50,
            DecompressedBytesPerIteration: 75,
            BatchesPerIteration: 1,
            EventsPerIteration: 10,
            PayloadBytesPerIteration: 10,
            PayloadValuesPerIteration: 10,
            RawValueChecksumPerIteration: 1,
            TopologyVersionCount: 1,
            RebalanceEvaluationCount: 2,
            AcceptedMoveCount: 1,
            SkippedDecisionCount: 0,
            DirectHotReliefCount: 1,
            ColdEvacuationCount: 0,
            FailedMigrationCount: 0,
            ValidationSucceeded: true,
            ValidationChecksum: 1,
            SkippedReasons: Array.Empty<RadarProcessingRebalanceSkippedReason>(),
            AcceptedMovePressures: Array.Empty<RadarProcessingSyntheticRebalanceMovePressure>(),
            Elapsed: TimeSpan.FromMilliseconds(10),
            ProcessingElapsed: TimeSpan.FromMilliseconds(4),
            AllocatedBytes: 1_000,
            ValidationProfile: RadarProcessingValidationProfile.Benchmark,
            RetentionMode: RadarProcessingDiagnosticRetentionMode.Counters,
            AllocationSummary: RadarProcessingRebalanceAllocationSummary.ForArchiveReplay(1_000, 400));

        Assert.Equal(1_000, result.AllocatedBytes);
        Assert.Equal(400, result.ProcessingCallbackAllocatedBytes);
        Assert.Equal(600, result.ReplayAndBatchConstructionAllocatedBytes);
        Assert.Equal(RadarProcessingValidationProfile.Benchmark, result.ValidationProfile);
        Assert.Equal(RadarProcessingDiagnosticRetentionMode.Counters, result.RetentionMode);
        Assert.True(result.AllocationSummary.IncludesArchiveReplayAndBatchConstruction);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    [Fact]
    public void AllocationContractsRejectNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingBenchmarkAllocationSnapshot(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRebalanceAllocationSummary.ForProcessingOnly(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingRebalanceAllocationSummary.ForArchiveReplay(1, -1));
    }
}
