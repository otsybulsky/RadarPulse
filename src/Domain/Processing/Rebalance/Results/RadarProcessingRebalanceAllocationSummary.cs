namespace RadarPulse.Domain.Processing;

/// <summary>
/// Allocation measurement breakdown for rebalance benchmark and replay runs.
/// </summary>
/// <remarks>
/// The summary separates callback allocations from archive replay and owned
/// snapshot allocation where those measurements are available, allowing reports
/// to avoid attributing setup cost to processing-only rebalance work.
/// </remarks>
public readonly record struct RadarProcessingRebalanceAllocationSummary
{
    /// <summary>
    /// Creates an allocation summary.
    /// </summary>
    public RadarProcessingRebalanceAllocationSummary(
        long measuredAllocatedBytes,
        long processingCallbackAllocatedBytes,
        bool includesArchiveReplayAndBatchConstruction,
        bool includesCliFormatting,
        long ownedSnapshotAllocatedBytes = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(measuredAllocatedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(processingCallbackAllocatedBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotAllocatedBytes);

        IsMeasured = true;
        MeasuredAllocatedBytes = measuredAllocatedBytes;
        ProcessingCallbackAllocatedBytes = processingCallbackAllocatedBytes;
        IncludesArchiveReplayAndBatchConstruction = includesArchiveReplayAndBatchConstruction;
        IncludesCliFormatting = includesCliFormatting;
        OwnedSnapshotAllocatedBytes = ownedSnapshotAllocatedBytes;
    }

    /// <summary>
    /// Indicates whether allocation was measured.
    /// </summary>
    public bool IsMeasured { get; }

    /// <summary>
    /// Total measured allocated bytes.
    /// </summary>
    public long MeasuredAllocatedBytes { get; }

    /// <summary>
    /// Bytes attributed to the processing callback.
    /// </summary>
    public long ProcessingCallbackAllocatedBytes { get; }

    /// <summary>
    /// Indicates whether the measurement also includes archive replay and batch construction.
    /// </summary>
    public bool IncludesArchiveReplayAndBatchConstruction { get; }

    /// <summary>
    /// Indicates whether CLI formatting is included in the measurement.
    /// </summary>
    public bool IncludesCliFormatting { get; }

    /// <summary>
    /// Bytes attributed to owned snapshot creation inside the processing callback.
    /// </summary>
    public long OwnedSnapshotAllocatedBytes { get; }

    /// <summary>
    /// Indicates whether processing callback allocation was measured separately from replay setup.
    /// </summary>
    public bool HasSeparateProcessingCallbackAllocation =>
        IncludesArchiveReplayAndBatchConstruction;

    /// <summary>
    /// Bytes attributed to archive replay and batch construction.
    /// </summary>
    public long ReplayAndBatchConstructionAllocatedBytes =>
        IncludesArchiveReplayAndBatchConstruction && MeasuredAllocatedBytes > ProcessingCallbackAllocatedBytes
            ? MeasuredAllocatedBytes - ProcessingCallbackAllocatedBytes
            : 0;

    /// <summary>
    /// Processing callback allocation excluding owned snapshot allocation.
    /// </summary>
    public long ProcessingCallbackNonOwnedSnapshotAllocatedBytes =>
        ProcessingCallbackAllocatedBytes > OwnedSnapshotAllocatedBytes
            ? ProcessingCallbackAllocatedBytes - OwnedSnapshotAllocatedBytes
            : 0;

    /// <summary>
    /// Creates a processing-only allocation summary.
    /// </summary>
    public static RadarProcessingRebalanceAllocationSummary ForProcessingOnly(
        long measuredAllocatedBytes) =>
        new(
            measuredAllocatedBytes,
            processingCallbackAllocatedBytes: measuredAllocatedBytes,
            includesArchiveReplayAndBatchConstruction: false,
            includesCliFormatting: false);

    /// <summary>
    /// Creates an archive replay allocation summary with separate processing callback bytes.
    /// </summary>
    public static RadarProcessingRebalanceAllocationSummary ForArchiveReplay(
        long measuredAllocatedBytes,
        long processingCallbackAllocatedBytes,
        long ownedSnapshotAllocatedBytes = 0) =>
        new(
            measuredAllocatedBytes,
            processingCallbackAllocatedBytes,
            includesArchiveReplayAndBatchConstruction: true,
            includesCliFormatting: false,
            ownedSnapshotAllocatedBytes);

    /// <summary>
    /// Returns total measured bytes per payload value.
    /// </summary>
    public double MeasuredAllocatedBytesPerPayloadValue(
        long payloadValueCount) =>
        Ratio(MeasuredAllocatedBytes, payloadValueCount);

    /// <summary>
    /// Returns processing callback bytes per payload value.
    /// </summary>
    public double ProcessingCallbackAllocatedBytesPerPayloadValue(
        long payloadValueCount) =>
        Ratio(ProcessingCallbackAllocatedBytes, payloadValueCount);

    /// <summary>
    /// Returns processing callback bytes per rebalance evaluation.
    /// </summary>
    public double ProcessingCallbackAllocatedBytesPerRebalanceEvaluation(
        long rebalanceEvaluationCount) =>
        Ratio(ProcessingCallbackAllocatedBytes, rebalanceEvaluationCount);

    /// <summary>
    /// Returns replay and batch construction bytes per payload value.
    /// </summary>
    public double ReplayAndBatchConstructionAllocatedBytesPerPayloadValue(
        long payloadValueCount) =>
        Ratio(ReplayAndBatchConstructionAllocatedBytes, payloadValueCount);

    /// <summary>
    /// Returns owned snapshot bytes per payload value.
    /// </summary>
    public double OwnedSnapshotAllocatedBytesPerPayloadValue(
        long payloadValueCount) =>
        Ratio(OwnedSnapshotAllocatedBytes, payloadValueCount);

    /// <summary>
    /// Returns processing callback bytes excluding owned snapshot allocation per payload value.
    /// </summary>
    public double ProcessingCallbackNonOwnedSnapshotAllocatedBytesPerPayloadValue(
        long payloadValueCount) =>
        Ratio(ProcessingCallbackNonOwnedSnapshotAllocatedBytes, payloadValueCount);

    private static double Ratio(
        long numerator,
        long denominator) =>
        denominator <= 0 ? 0 : (double)numerator / denominator;
}
