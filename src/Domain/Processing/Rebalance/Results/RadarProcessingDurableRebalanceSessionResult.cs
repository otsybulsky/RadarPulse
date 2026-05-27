namespace RadarPulse.Domain.Processing;

/// <summary>
/// Durable queued-session result that includes rebalance topology posture.
/// </summary>
public sealed class RadarProcessingDurableRebalanceSessionResult
{
    private readonly IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> processingResults;

    /// <summary>
    /// Creates a durable rebalance session result.
    /// </summary>
    public RadarProcessingDurableRebalanceSessionResult(
        RadarProcessingQueuedSessionStatus status,
        RadarProcessingDurableQueueSummary? queueSummary = null,
        IReadOnlyCollection<RadarProcessingQueuedBatchProcessingResult>? processingResults = null,
        string message = "",
        RadarProcessingTopologyVersion? finalTopologyVersion = null)
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
        FinalTopologyVersion = finalTopologyVersion;
    }

    /// <summary>
    /// Final durable queued-session status.
    /// </summary>
    public RadarProcessingQueuedSessionStatus Status { get; }

    /// <summary>
    /// Durable queue summary captured at completion.
    /// </summary>
    public RadarProcessingDurableQueueSummary QueueSummary { get; }

    /// <summary>
    /// Runtime readiness derived from durable queue posture.
    /// </summary>
    public RadarProcessingDurableRuntimeReadinessSummary ReadinessSummary { get; }

    /// <summary>
    /// Per-batch processing results committed during the session.
    /// </summary>
    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> ProcessingResults => processingResults;

    /// <summary>
    /// Operator-facing completion or failure message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Last topology version observed by the durable rebalance session.
    /// </summary>
    public RadarProcessingTopologyVersion? FinalTopologyVersion { get; }

    /// <summary>
    /// Indicates successful session completion.
    /// </summary>
    public bool IsCompleted => Status == RadarProcessingQueuedSessionStatus.Completed;

    /// <summary>
    /// Indicates session failure.
    /// </summary>
    public bool IsFaulted => Status == RadarProcessingQueuedSessionStatus.Faulted;

    /// <summary>
    /// Indicates session cancellation.
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
