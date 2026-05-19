namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingRebalanceAllocationSummary
{
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

    public bool IsMeasured { get; }

    public long MeasuredAllocatedBytes { get; }

    public long ProcessingCallbackAllocatedBytes { get; }

    public bool IncludesArchiveReplayAndBatchConstruction { get; }

    public bool IncludesCliFormatting { get; }

    public long OwnedSnapshotAllocatedBytes { get; }

    public bool HasSeparateProcessingCallbackAllocation =>
        IncludesArchiveReplayAndBatchConstruction;

    public long ReplayAndBatchConstructionAllocatedBytes =>
        IncludesArchiveReplayAndBatchConstruction && MeasuredAllocatedBytes > ProcessingCallbackAllocatedBytes
            ? MeasuredAllocatedBytes - ProcessingCallbackAllocatedBytes
            : 0;

    public static RadarProcessingRebalanceAllocationSummary ForProcessingOnly(
        long measuredAllocatedBytes) =>
        new(
            measuredAllocatedBytes,
            processingCallbackAllocatedBytes: measuredAllocatedBytes,
            includesArchiveReplayAndBatchConstruction: false,
            includesCliFormatting: false);

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

    public double MeasuredAllocatedBytesPerPayloadValue(
        long payloadValueCount) =>
        Ratio(MeasuredAllocatedBytes, payloadValueCount);

    public double ProcessingCallbackAllocatedBytesPerPayloadValue(
        long payloadValueCount) =>
        Ratio(ProcessingCallbackAllocatedBytes, payloadValueCount);

    public double ProcessingCallbackAllocatedBytesPerRebalanceEvaluation(
        long rebalanceEvaluationCount) =>
        Ratio(ProcessingCallbackAllocatedBytes, rebalanceEvaluationCount);

    public double ReplayAndBatchConstructionAllocatedBytesPerPayloadValue(
        long payloadValueCount) =>
        Ratio(ReplayAndBatchConstructionAllocatedBytes, payloadValueCount);

    public double OwnedSnapshotAllocatedBytesPerPayloadValue(
        long payloadValueCount) =>
        Ratio(OwnedSnapshotAllocatedBytes, payloadValueCount);

    private static double Ratio(
        long numerator,
        long denominator) =>
        denominator <= 0 ? 0 : (double)numerator / denominator;
}
