using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingArchiveRebalanceCacheBenchmarkResult(
    string CachePath,
    DateOnly? Date,
    string? RadarId,
    string Decompressor,
    RadarProcessingSyntheticRebalanceBenchmarkMode Mode,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    int SourceCount,
    int PartitionCount,
    int ShardCount,
    long ExaminedFilesPerIteration,
    long SkippedFilesPerIteration,
    long PublishedFilesPerIteration,
    long FileSizeBytesPerIteration,
    long CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    long BatchesPerIteration,
    long EventsPerIteration,
    long PayloadBytesPerIteration,
    long PayloadValuesPerIteration,
    long RawValueChecksumPerIteration,
    long TopologyVersionCount,
    long RebalanceEvaluationCount,
    long AcceptedMoveCount,
    long SkippedDecisionCount,
    long DirectHotReliefCount,
    long ColdEvacuationCount,
    long FailedMigrationCount,
    bool ValidationSucceeded,
    ulong ValidationChecksum,
    IReadOnlyList<RadarProcessingRebalanceSkippedReason> SkippedReasons,
    IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> SkippedReasonCounters,
    IReadOnlyList<RadarProcessingSyntheticRebalanceMovePressure> AcceptedMovePressures,
    RadarProcessingRebalanceRetentionStats RetentionStats,
    TimeSpan Elapsed,
    TimeSpan ProcessingElapsed,
    long AllocatedBytes,
    RadarProcessingValidationProfile ValidationProfile = RadarProcessingValidationProfile.Diagnostic,
    RadarProcessingDiagnosticRetentionMode RetentionMode = RadarProcessingDiagnosticRetentionMode.Recent,
    int QuarantineTtlEvaluations = 64,
    int QuarantineSustainedCoolingSampleCount = 3,
    double QuarantineMaterialPressureChangeThreshold = 0.25,
    int MaxRetainedDecisions = 128,
    int MaxRetainedLifecycleTransitions = 64,
    int MaxRetainedAcceptedMoves = 64,
    int MaxRetainedValidationFailures = 32,
    RadarProcessingPressureSkewOptions? PressureSkew = null,
    RadarProcessingRebalanceAllocationSummary AllocationSummary = default,
    RadarProcessingExecutionMode ExecutionMode = RadarProcessingExecutionMode.PartitionedBarrier,
    RadarProcessingWorkerTelemetrySummary? WorkerTelemetry = null,
    RadarProcessingArchiveProviderMode ProviderMode = RadarProcessingArchiveProviderMode.BlockingBorrowed,
    int QueueCapacity = 0,
    RadarProcessingProviderQueueTelemetrySummary? QueueTelemetry = null)
{
    public bool HasWorkerTelemetry => WorkerTelemetry is not null;

    public bool HasQueueTelemetry => ProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned;

    public IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> SkippedReasonCounters { get; init; } =
        SkippedReasonCounters;

    public RadarProcessingRebalanceRetentionStats RetentionStats { get; init; } = RetentionStats;

    public int MaxRetainedDecisions { get; init; } = MaxRetainedDecisions;

    public int MaxRetainedLifecycleTransitions { get; init; } = MaxRetainedLifecycleTransitions;

    public int MaxRetainedAcceptedMoves { get; init; } = MaxRetainedAcceptedMoves;

    public int MaxRetainedValidationFailures { get; init; } = MaxRetainedValidationFailures;

    public RadarProcessingPressureSkewOptions PressureSkew { get; init; } =
        PressureSkew ?? RadarProcessingPressureSkewOptions.None;

    public RadarProcessingProviderQueueTelemetrySummary QueueTelemetry { get; init; } =
        QueueTelemetry ?? RadarProcessingProviderQueueTelemetrySummary.Empty;

    public long TotalExaminedFiles => ExaminedFilesPerIteration * Iterations;

    public long TotalSkippedFiles => SkippedFilesPerIteration * Iterations;

    public long TotalPublishedFiles => PublishedFilesPerIteration * Iterations;

    public long TotalFileSizeBytes => FileSizeBytesPerIteration * Iterations;

    public long TotalCompressedRecords => CompressedRecordsPerIteration * Iterations;

    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;

    public long TotalBatches => BatchesPerIteration * Iterations;

    public long TotalEvents => EventsPerIteration * Iterations;

    public long TotalPayloadBytes => PayloadBytesPerIteration * Iterations;

    public long TotalPayloadValues => PayloadValuesPerIteration * Iterations;

    public TimeSpan ReplayAndBatchConstructionElapsed =>
        Elapsed >= ProcessingElapsed ? Elapsed - ProcessingElapsed : TimeSpan.Zero;

    public double CompressedMegabytesPerSecond => MegabytesPerSecond(TotalCompressedBytes, Elapsed);

    public double DecompressedMegabytesPerSecond => MegabytesPerSecond(TotalDecompressedBytes, Elapsed);

    public double FilesPerSecond => PerSecond(TotalPublishedFiles, Elapsed);

    public double BatchesPerSecond => PerSecond(TotalBatches, Elapsed);

    public double EventsPerSecond => PerSecond(TotalEvents, Elapsed);

    public double PayloadValuesPerSecond => PerSecond(TotalPayloadValues, Elapsed);

    public double ProcessingEventsPerSecond => PerSecond(TotalEvents, ProcessingElapsed);

    public double ProcessingPayloadValuesPerSecond => PerSecond(TotalPayloadValues, ProcessingElapsed);

    public double RebalanceEvaluationsPerSecond => PerSecond(RebalanceEvaluationCount, ProcessingElapsed);

    public double AllocatedBytesPerStreamEvent => Ratio(AllocatedBytes, TotalEvents);

    public double AllocatedBytesPerPayloadValue => Ratio(AllocatedBytes, TotalPayloadValues);

    public double AllocatedBytesPerRebalanceEvaluation => Ratio(AllocatedBytes, RebalanceEvaluationCount);

    public long ProcessingCallbackAllocatedBytes => AllocationSummary.ProcessingCallbackAllocatedBytes;

    public long ReplayAndBatchConstructionAllocatedBytes =>
        AllocationSummary.ReplayAndBatchConstructionAllocatedBytes;

    public double ProcessingCallbackAllocatedBytesPerPayloadValue =>
        AllocationSummary.ProcessingCallbackAllocatedBytesPerPayloadValue(TotalPayloadValues);

    public double ProcessingCallbackAllocatedBytesPerRebalanceEvaluation =>
        AllocationSummary.ProcessingCallbackAllocatedBytesPerRebalanceEvaluation(RebalanceEvaluationCount);

    public double ReplayAndBatchConstructionAllocatedBytesPerPayloadValue =>
        AllocationSummary.ReplayAndBatchConstructionAllocatedBytesPerPayloadValue(TotalPayloadValues);

    public long OwnedSnapshotAllocatedBytes => AllocationSummary.OwnedSnapshotAllocatedBytes;

    public double OwnedSnapshotAllocatedBytesPerPayloadValue =>
        AllocationSummary.OwnedSnapshotAllocatedBytesPerPayloadValue(TotalPayloadValues);

    public TimeSpan OwnedSnapshotElapsed => QueueTelemetry.TotalOwnedSnapshotTime;

    public TimeSpan EnqueueWaitElapsed => QueueTelemetry.TotalEnqueueWaitTime;

    public TimeSpan QueueDrainElapsed => QueueTelemetry.TotalDrainTime;

    private static double MegabytesPerSecond(
        long bytes,
        TimeSpan elapsed) =>
        elapsed.TotalSeconds <= 0 ? 0 : bytes / 1_000_000d / elapsed.TotalSeconds;

    private static double PerSecond(
        long value,
        TimeSpan elapsed) =>
        elapsed.TotalSeconds <= 0 ? 0 : value / elapsed.TotalSeconds;

    private static double Ratio(
        long numerator,
        long denominator) =>
        denominator <= 0 ? 0 : (double)numerator / denominator;
}
