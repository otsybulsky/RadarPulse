namespace RadarPulse.Domain.Processing;

public enum RadarProcessingRetainedBatchResourceState
{
    ProviderOwned = 1,
    QueueOwned = 2,
    ConsumerOwned = 3,
    Released = 4,
    ReleaseFailed = 5
}
