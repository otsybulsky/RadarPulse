namespace RadarPulse.Domain.Processing;

/// <summary>
/// Aggregate result for a queued-provider processing session.
/// </summary>
/// <remarks>
/// The result keeps enqueue evidence, processing evidence, queue telemetry, and
/// final topology together so validation and readiness checks can reason about
/// the entire queued-provider contour.
/// </remarks>
public sealed class RadarProcessingQueuedSessionResult
{
    private readonly IReadOnlyList<RadarProcessingQueuedBatchEnqueueResult> enqueueResults;
    private readonly IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> processingResults;

    public RadarProcessingQueuedSessionResult(
        RadarProcessingQueuedSessionStatus status,
        RadarProcessingProviderQueueTelemetrySummary? telemetry = null,
        IReadOnlyCollection<RadarProcessingQueuedBatchEnqueueResult>? enqueueResults = null,
        IReadOnlyCollection<RadarProcessingQueuedBatchProcessingResult>? processingResults = null,
        string message = "",
        RadarProcessingTopologyVersion? finalTopologyVersion = null)
    {
        EnsureKnownStatus(status);
        ArgumentNullException.ThrowIfNull(message);

        Status = status;
        Telemetry = telemetry ?? RadarProcessingProviderQueueTelemetrySummary.Empty;
        this.enqueueResults = CopyRequired(enqueueResults ?? Array.Empty<RadarProcessingQueuedBatchEnqueueResult>(), nameof(enqueueResults));
        this.processingResults = CopyRequired(processingResults ?? Array.Empty<RadarProcessingQueuedBatchProcessingResult>(), nameof(processingResults));
        Message = message;
        FinalTopologyVersion = finalTopologyVersion;
    }

    /// <summary>
    /// Terminal or current session status.
    /// </summary>
    public RadarProcessingQueuedSessionStatus Status { get; }

    /// <summary>
    /// Queue telemetry captured for the session.
    /// </summary>
    public RadarProcessingProviderQueueTelemetrySummary Telemetry { get; }

    /// <summary>
    /// Enqueue results captured in provider sequence order.
    /// </summary>
    public IReadOnlyList<RadarProcessingQueuedBatchEnqueueResult> EnqueueResults => enqueueResults;

    /// <summary>
    /// Processing results captured for dequeued batches.
    /// </summary>
    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> ProcessingResults => processingResults;

    /// <summary>
    /// Optional terminal diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Final topology version reached by the session when available.
    /// </summary>
    public RadarProcessingTopologyVersion? FinalTopologyVersion { get; }

    /// <summary>
    /// Indicates a completed terminal state.
    /// </summary>
    public bool IsCompleted => Status == RadarProcessingQueuedSessionStatus.Completed;

    /// <summary>
    /// Indicates a faulted terminal state.
    /// </summary>
    public bool IsFaulted => Status == RadarProcessingQueuedSessionStatus.Faulted;

    /// <summary>
    /// Indicates a canceled terminal state.
    /// </summary>
    public bool IsCanceled => Status == RadarProcessingQueuedSessionStatus.Canceled;

    internal static void EnsureKnownStatus(
        RadarProcessingQueuedSessionStatus status)
    {
        if (status is not RadarProcessingQueuedSessionStatus.NotStarted and
            not RadarProcessingQueuedSessionStatus.Running and
            not RadarProcessingQueuedSessionStatus.Draining and
            not RadarProcessingQueuedSessionStatus.Completed and
            not RadarProcessingQueuedSessionStatus.Faulted and
            not RadarProcessingQueuedSessionStatus.Canceled and
            not RadarProcessingQueuedSessionStatus.Disposed)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private static IReadOnlyList<T> CopyRequired<T>(
        IReadOnlyCollection<T> values,
        string paramName)
        where T : class
    {
        if (values.Count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new List<T>(values.Count);
        foreach (var value in values)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }

            result.Add(value);
        }

        return Array.AsReadOnly(result.ToArray());
    }
}
