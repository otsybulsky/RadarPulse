using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingArchiveQueuedOverlapOptions
{
    public static RadarProcessingArchiveQueuedOverlapOptions Default { get; } = CreateRuntimeDefault();

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

    public RadarProcessingProviderQueueOptions QueueOptions { get; }

    public RadarProcessingRetainedPayloadOptions RetainedPayloadOptions { get; }

    public RadarProcessingRetainedPayloadFactory? RetainedPayloadFactory { get; }

    public RadarProcessingRetainedPayloadPrewarmOptions RetainedPayloadPrewarmOptions { get; }

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
