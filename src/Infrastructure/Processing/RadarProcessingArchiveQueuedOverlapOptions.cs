using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingArchiveQueuedOverlapOptions
{
    public static RadarProcessingArchiveQueuedOverlapOptions Default { get; } = new();

    public RadarProcessingArchiveQueuedOverlapOptions(
        RadarProcessingProviderQueueOptions? queueOptions = null,
        RadarProcessingRetainedPayloadOptions? retainedPayloadOptions = null,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory = null)
    {
        QueueOptions = queueOptions ?? RadarProcessingProviderQueueOptions.Default;
        RetainedPayloadOptions = retainedPayloadOptions ?? RadarProcessingRetainedPayloadOptions.Default;
        RetainedPayloadFactory = retainedPayloadFactory;
    }

    public RadarProcessingProviderQueueOptions QueueOptions { get; }

    public RadarProcessingRetainedPayloadOptions RetainedPayloadOptions { get; }

    public RadarProcessingRetainedPayloadFactory? RetainedPayloadFactory { get; }
}
