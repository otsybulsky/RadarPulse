namespace RadarPulse.Domain.Processing;

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

    public RadarProcessingQueuedSessionStatus Status { get; }

    public RadarProcessingProviderQueueTelemetrySummary Telemetry { get; }

    public IReadOnlyList<RadarProcessingQueuedBatchEnqueueResult> EnqueueResults => enqueueResults;

    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> ProcessingResults => processingResults;

    public string Message { get; }

    public RadarProcessingTopologyVersion? FinalTopologyVersion { get; }

    public bool IsCompleted => Status == RadarProcessingQueuedSessionStatus.Completed;

    public bool IsFaulted => Status == RadarProcessingQueuedSessionStatus.Faulted;

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
