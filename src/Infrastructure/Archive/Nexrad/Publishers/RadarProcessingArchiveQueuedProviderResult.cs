using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Archive;

public sealed class RadarProcessingArchiveQueuedProviderResult
{
    private readonly IReadOnlyList<RadarProcessingQueuedBatchEnqueueResult> enqueueResults;

    public RadarProcessingArchiveQueuedProviderResult(
        IReadOnlyCollection<RadarProcessingQueuedBatchEnqueueResult>? enqueueResults = null,
        RadarProcessingProviderQueueTelemetrySummary? telemetry = null,
        RadarProcessingRetainedPayloadTelemetrySummary? retentionTelemetry = null)
    {
        this.enqueueResults = CopyRequired(
            enqueueResults ?? Array.Empty<RadarProcessingQueuedBatchEnqueueResult>(),
            nameof(enqueueResults));
        Telemetry = telemetry ?? RadarProcessingProviderQueueTelemetrySummary.Empty;
        RetentionTelemetry = retentionTelemetry ?? RadarProcessingRetainedPayloadTelemetrySummary.Empty;
    }

    public IReadOnlyList<RadarProcessingQueuedBatchEnqueueResult> EnqueueResults => enqueueResults;

    public RadarProcessingProviderQueueTelemetrySummary Telemetry { get; }

    public RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry { get; }

    public long PublishAttemptCount => enqueueResults.Count;

    public long AcceptedPublishCount => enqueueResults.LongCount(static result => result.IsAccepted);

    public long RejectedPublishCount => PublishAttemptCount - AcceptedPublishCount;

    public bool HasRejectedPublish => RejectedPublishCount > 0;

    public RadarProcessingQueuedBatchEnqueueResult? LastEnqueueResult =>
        enqueueResults.Count == 0 ? null : enqueueResults[^1];

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
