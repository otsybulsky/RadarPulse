using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingArchiveQueuedOverlapOptions
{
    public static RadarProcessingArchiveQueuedOverlapOptions Default { get; } = new();

    public RadarProcessingArchiveQueuedOverlapOptions(
        RadarProcessingProviderQueueOptions? queueOptions = null)
    {
        QueueOptions = queueOptions ?? RadarProcessingProviderQueueOptions.Default;
    }

    public RadarProcessingProviderQueueOptions QueueOptions { get; }
}
