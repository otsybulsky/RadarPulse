namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingWorkerMailboxOptions
{
    public static RadarProcessingWorkerMailboxOptions Default { get; } = new();

    public RadarProcessingWorkerMailboxOptions(int capacity = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        Capacity = capacity;
    }

    public int Capacity { get; }
}
