using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Result snapshot for archive batch publishing into the processing owned-batch queue.
/// </summary>
public sealed class RadarProcessingArchiveQueuedProviderResult
{
    private readonly IReadOnlyList<RadarProcessingQueuedBatchEnqueueResult> enqueueResults;

    /// <summary>
    /// Creates a queueing provider result from enqueue outcomes and telemetry snapshots.
    /// </summary>
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

    /// <summary>
    /// Gets the immutable enqueue results captured during archive publishing.
    /// </summary>
    public IReadOnlyList<RadarProcessingQueuedBatchEnqueueResult> EnqueueResults => enqueueResults;

    /// <summary>
    /// Gets processing queue telemetry recorded during publishing.
    /// </summary>
    public RadarProcessingProviderQueueTelemetrySummary Telemetry { get; }

    /// <summary>
    /// Gets retained-payload telemetry recorded while queueing owned batches.
    /// </summary>
    public RadarProcessingRetainedPayloadTelemetrySummary RetentionTelemetry { get; }

    /// <summary>
    /// Gets the number of attempted batch publishes.
    /// </summary>
    public long PublishAttemptCount => enqueueResults.Count;

    /// <summary>
    /// Gets the number of accepted batch publishes.
    /// </summary>
    public long AcceptedPublishCount => enqueueResults.LongCount(static result => result.IsAccepted);

    /// <summary>
    /// Gets the number of rejected batch publishes.
    /// </summary>
    public long RejectedPublishCount => PublishAttemptCount - AcceptedPublishCount;

    /// <summary>
    /// Gets whether any batch publish was rejected.
    /// </summary>
    public bool HasRejectedPublish => RejectedPublishCount > 0;

    /// <summary>
    /// Gets the last enqueue result, or <see langword="null"/> when no publish was attempted.
    /// </summary>
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
