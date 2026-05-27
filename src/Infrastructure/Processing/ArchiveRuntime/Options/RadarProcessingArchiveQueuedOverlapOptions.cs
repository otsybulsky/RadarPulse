using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Options for archive producer and processing consumer overlap.
/// </summary>
public sealed record RadarProcessingArchiveQueuedOverlapOptions
{
    /// <summary>
    /// Accepted runtime default contour for queued-owned archive processing.
    /// </summary>
    public static RadarProcessingArchiveQueuedOverlapOptions Default { get; } = CreateRuntimeDefault();

    /// <summary>
    /// Creates overlap options for provider queue, retention, and optional prewarm.
    /// </summary>
    public RadarProcessingArchiveQueuedOverlapOptions(
        RadarProcessingProviderQueueOptions? queueOptions = null,
        RadarProcessingRetainedPayloadOptions? retainedPayloadOptions = null,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory = null,
        RadarProcessingRetainedPayloadPrewarmOptions? retainedPayloadPrewarmOptions = null)
    {
        QueueOptions = queueOptions ?? RadarProcessingProviderQueueOptions.Default;
        RetainedPayloadOptions = retainedPayloadOptions ?? RadarProcessingRetainedPayloadOptions.Default;
        RetainedPayloadFactory = retainedPayloadFactory;
        RetainedPayloadPrewarmOptions =
            retainedPayloadPrewarmOptions ?? RadarProcessingRetainedPayloadPrewarmOptions.None;

        if (RetainedPayloadPrewarmOptions.Enabled &&
            RetainedPayloadOptions.Strategy != RadarProcessingRetainedPayloadStrategy.PooledCopy)
        {
            throw new InvalidOperationException(
                "Retained payload prewarm requires pooled-copy retained payload strategy.");
        }
    }

    /// <summary>
    /// Provider queue options used between producer and consumer.
    /// </summary>
    public RadarProcessingProviderQueueOptions QueueOptions { get; }

    /// <summary>
    /// Retained payload strategy and budget used by the queueing publisher.
    /// </summary>
    public RadarProcessingRetainedPayloadOptions RetainedPayloadOptions { get; }

    /// <summary>
    /// Optional retained payload factory supplied by callers.
    /// </summary>
    public RadarProcessingRetainedPayloadFactory? RetainedPayloadFactory { get; }

    /// <summary>
    /// Startup prewarm settings for retained payload pools.
    /// </summary>
    public RadarProcessingRetainedPayloadPrewarmOptions RetainedPayloadPrewarmOptions { get; }

    /// <summary>
    /// Indicates whether options match the accepted runtime default contour.
    /// </summary>
    public bool IsRuntimeDefaultContour =>
        QueueOptions.Capacity == RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity &&
        QueueOptions.MaxRetainedPayloadBytes == RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes &&
        RetainedPayloadOptions.Strategy == RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy &&
        RetainedPayloadOptions.MaxRetainedPayloadBytes == RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes &&
        RetainedPayloadPrewarmOptions == RadarProcessingRetainedPayloadPrewarmOptions.RolloutDefault;

    private static RadarProcessingArchiveQueuedOverlapOptions CreateRuntimeDefault() =>
        new(
            new RadarProcessingProviderQueueOptions(
                capacity: RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity,
                maxRetainedPayloadBytes: RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes),
            new RadarProcessingRetainedPayloadOptions(
                RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy,
                RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes),
            retainedPayloadPrewarmOptions: RadarProcessingRetainedPayloadPrewarmOptions.RolloutDefault);
}
