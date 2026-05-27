namespace RadarPulse.Domain.Processing;

/// <summary>
/// Aggregate result for durable processing over claimed envelopes.
/// </summary>
/// <remarks>
/// The result combines queued-session status, durable queue summary, readiness
/// summary, and per-batch processing results so recovery decisions can inspect
/// both runtime outcome and persistent queue posture.
/// </remarks>
public sealed class RadarProcessingDurableProcessingSessionResult
{
    private readonly IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> processingResults;

    public RadarProcessingDurableProcessingSessionResult(
        RadarProcessingQueuedSessionStatus status,
        RadarProcessingDurableQueueSummary? queueSummary = null,
        IReadOnlyCollection<RadarProcessingQueuedBatchProcessingResult>? processingResults = null,
        string message = "")
    {
        RadarProcessingQueuedSessionResult.EnsureKnownStatus(status);
        ArgumentNullException.ThrowIfNull(message);

        Status = status;
        QueueSummary = queueSummary ?? RadarProcessingDurableQueueSummary.Empty;
        ReadinessSummary = new RadarProcessingDurableRuntimeReadinessSummary(QueueSummary);
        this.processingResults = CopyRequired(
            processingResults ?? Array.Empty<RadarProcessingQueuedBatchProcessingResult>(),
            nameof(processingResults));
        Message = message;
    }

    /// <summary>
    /// Durable processing session status.
    /// </summary>
    public RadarProcessingQueuedSessionStatus Status { get; }

    /// <summary>
    /// Durable queue summary captured at session end.
    /// </summary>
    public RadarProcessingDurableQueueSummary QueueSummary { get; }

    /// <summary>
    /// Readiness summary derived from durable queue evidence.
    /// </summary>
    public RadarProcessingDurableRuntimeReadinessSummary ReadinessSummary { get; }

    /// <summary>
    /// Processing results for claimed envelopes.
    /// </summary>
    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> ProcessingResults => processingResults;

    /// <summary>
    /// Optional terminal diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates a completed durable processing session.
    /// </summary>
    public bool IsCompleted => Status == RadarProcessingQueuedSessionStatus.Completed;

    /// <summary>
    /// Indicates a faulted durable processing session.
    /// </summary>
    public bool IsFaulted => Status == RadarProcessingQueuedSessionStatus.Faulted;

    /// <summary>
    /// Indicates a canceled durable processing session.
    /// </summary>
    public bool IsCanceled => Status == RadarProcessingQueuedSessionStatus.Canceled;

    private static IReadOnlyList<T> CopyRequired<T>(
        IReadOnlyCollection<T> values,
        string paramName)
        where T : class
    {
        if (values.Count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new T[values.Count];
        var index = 0;
        foreach (var value in values)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }

            result[index++] = value;
        }

        return Array.AsReadOnly(result);
    }
}
