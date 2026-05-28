namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of processing one dequeued provider batch.
/// </summary>
/// <remarks>
/// Successful results carry either direct processing output or a rebalance result
/// that wraps the same processing output. This preserves the correlation between
/// provider sequence, processing topology version, and rebalance evidence.
/// </remarks>
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

    /// <summary>
    /// Provider sequence for the processed batch.
    /// </summary>
    public RadarProcessingQueuedBatchSequence Sequence { get; }

    /// <summary>
    /// Processing outcome.
    /// </summary>
    public RadarProcessingQueuedBatchProcessingStatus Status { get; }

    /// <summary>
    /// Processing output when available.
    /// </summary>
    public RadarProcessingResult? ProcessingResult { get; }

    /// <summary>
    /// Rebalance result when the batch was processed through a rebalance session.
    /// </summary>
    public RadarProcessingRebalanceSessionResult? RebalanceResult { get; }

    /// <summary>
    /// Diagnostic message for failed, canceled, or skipped results.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether the batch reached a successful processing outcome.
    /// </summary>
    public bool IsSuccessful => Status == RadarProcessingQueuedBatchProcessingStatus.Succeeded;

    /// <summary>
    /// Topology version from processing output when present.
    /// </summary>
    public RadarProcessingTopologyVersion? TopologyVersion => ProcessingResult?.TopologyVersion;

    /// <summary>
    /// Creates a successful result from direct processing output.
    /// </summary>
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

    /// <summary>
    /// Creates a successful result from rebalance processing output.
    /// </summary>
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

    /// <summary>
    /// Creates a processing-failure result.
    /// </summary>
    public static RadarProcessingQueuedBatchProcessingResult FailedProcessing(
        RadarProcessingQueuedBatchSequence sequence,
        string message,
        RadarProcessingResult? processingResult = null) =>
        Failed(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.FailedProcessing,
            message,
            processingResult);

    /// <summary>
    /// Creates a validation-failure result.
    /// </summary>
    public static RadarProcessingQueuedBatchProcessingResult FailedValidation(
        RadarProcessingQueuedBatchSequence sequence,
        string message,
        RadarProcessingResult? processingResult = null) =>
        Failed(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.FailedValidation,
            message,
            processingResult);

    /// <summary>
    /// Creates a migration-failure result.
    /// </summary>
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

    /// <summary>
    /// Creates a canceled processing result.
    /// </summary>
    public static RadarProcessingQueuedBatchProcessingResult Canceled(
        RadarProcessingQueuedBatchSequence sequence,
        string message = "") =>
        new(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.Canceled,
            null,
            null,
            message);

    /// <summary>
    /// Creates a skipped result for batches not processed after a prior fault.
    /// </summary>
    public static RadarProcessingQueuedBatchProcessingResult SkippedAfterFault(
        RadarProcessingQueuedBatchSequence sequence,
        string message = "") =>
        new(
            sequence,
            RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault,
            null,
            null,
            message);

    /// <summary>
    /// Throws when a queued batch processing status is outside the known contract.
    /// </summary>
    public static void EnsureKnownStatus(
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
