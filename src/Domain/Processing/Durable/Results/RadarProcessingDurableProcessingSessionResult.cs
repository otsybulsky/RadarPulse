namespace RadarPulse.Domain.Processing;

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

    public RadarProcessingQueuedSessionStatus Status { get; }

    public RadarProcessingDurableQueueSummary QueueSummary { get; }

    public RadarProcessingDurableRuntimeReadinessSummary ReadinessSummary { get; }

    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> ProcessingResults => processingResults;

    public string Message { get; }

    public bool IsCompleted => Status == RadarProcessingQueuedSessionStatus.Completed;

    public bool IsFaulted => Status == RadarProcessingQueuedSessionStatus.Faulted;

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
