namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingQueuedBatchProcessingResult
{
    private RadarProcessingQueuedBatchProcessingResult(
        RadarProcessingQueuedBatchSequence sequence,
        RadarProcessingQueuedBatchProcessingStatus status,
        RadarProcessingResult? processingResult,
        RadarProcessingRebalanceSessionResult? rebalanceResult,
        string message)
    {
        EnsureKnownStatus(status);
        ArgumentNullException.ThrowIfNull(message);

        if (status == RadarProcessingQueuedBatchProcessingStatus.Succeeded &&
            processingResult is null &&
            rebalanceResult is null)
        {
            throw new ArgumentException(
                "Successful queued processing results require processing output.",
                nameof(processingResult));
        }

        if (rebalanceResult is not null &&
            processingResult is not null &&
            !ReferenceEquals(rebalanceResult.ProcessingResult, processingResult))
        {
            throw new ArgumentException(
                "Queued rebalance result must wrap the supplied processing result.",
                nameof(rebalanceResult));
        }

        Sequence = sequence;
        Status = status;
        ProcessingResult = processingResult ?? rebalanceResult?.ProcessingResult;
        RebalanceResult = rebalanceResult;
        Message = message;
    }

    public RadarProcessingQueuedBatchSequence Sequence { get; }

    public RadarProcessingQueuedBatchProcessingStatus Status { get; }

    public RadarProcessingResult? ProcessingResult { get; }

    public RadarProcessingRebalanceSessionResult? RebalanceResult { get; }

    public string Message { get; }

    public bool IsSuccessful => Status == RadarProcessingQueuedBatchProcessingStatus.Succeeded;

    public RadarProcessingTopologyVersion? TopologyVersion => ProcessingResult?.TopologyVersion;

    public static RadarProcessingQueuedBatchProcessingResult Succeeded(
        RadarProcessingQueuedBatchSequence sequence,
        RadarProcessingResult processingResult)
    {
        ArgumentNullException.ThrowIfNull(processingResult);
        return new RadarProcessingQueuedBatchProcessingResult(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.Succeeded,
            processingResult,
            null,
            string.Empty);
    }

    public static RadarProcessingQueuedBatchProcessingResult Succeeded(
        RadarProcessingQueuedBatchSequence sequence,
        RadarProcessingRebalanceSessionResult rebalanceResult)
    {
        ArgumentNullException.ThrowIfNull(rebalanceResult);
        return new(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.Succeeded,
            rebalanceResult.ProcessingResult,
            rebalanceResult,
            string.Empty);
    }

    public static RadarProcessingQueuedBatchProcessingResult FailedProcessing(
        RadarProcessingQueuedBatchSequence sequence,
        string message,
        RadarProcessingResult? processingResult = null) =>
        Failed(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.FailedProcessing,
            message,
            processingResult);

    public static RadarProcessingQueuedBatchProcessingResult FailedValidation(
        RadarProcessingQueuedBatchSequence sequence,
        string message,
        RadarProcessingResult? processingResult = null) =>
        Failed(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.FailedValidation,
            message,
            processingResult);

    public static RadarProcessingQueuedBatchProcessingResult FailedMigration(
        RadarProcessingQueuedBatchSequence sequence,
        string message,
        RadarProcessingRebalanceSessionResult? rebalanceResult = null) =>
        new(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.FailedMigration,
            rebalanceResult?.ProcessingResult,
            rebalanceResult,
            message);

    public static RadarProcessingQueuedBatchProcessingResult Canceled(
        RadarProcessingQueuedBatchSequence sequence,
        string message = "") =>
        new(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.Canceled,
            null,
            null,
            message);

    public static RadarProcessingQueuedBatchProcessingResult SkippedAfterFault(
        RadarProcessingQueuedBatchSequence sequence,
        string message = "") =>
        new(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault,
            null,
            null,
            message);

    internal static void EnsureKnownStatus(
        RadarProcessingQueuedBatchProcessingStatus status)
    {
        if (status is not RadarProcessingQueuedBatchProcessingStatus.Succeeded and
            not RadarProcessingQueuedBatchProcessingStatus.FailedProcessing and
            not RadarProcessingQueuedBatchProcessingStatus.FailedValidation and
            not RadarProcessingQueuedBatchProcessingStatus.FailedMigration and
            not RadarProcessingQueuedBatchProcessingStatus.Canceled and
            not RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private static RadarProcessingQueuedBatchProcessingResult Failed(
        RadarProcessingQueuedBatchSequence sequence,
        RadarProcessingQueuedBatchProcessingStatus status,
        string message,
        RadarProcessingResult? processingResult) =>
        new(sequence, status, processingResult, null, message);
}
