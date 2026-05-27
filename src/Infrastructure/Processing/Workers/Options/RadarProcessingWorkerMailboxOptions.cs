namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Capacity settings for the single-reader worker mailbox used by async processing workers.
/// </summary>
public sealed record RadarProcessingWorkerMailboxOptions
{
    /// <summary>
    /// Default mailbox settings for one buffered work item per worker.
    /// </summary>
    public static RadarProcessingWorkerMailboxOptions Default { get; } = new();

    /// <summary>
    /// Creates mailbox settings with a bounded positive capacity.
    /// </summary>
    public RadarProcessingWorkerMailboxOptions(int capacity = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        Capacity = capacity;
    }

    /// <summary>
    /// Maximum number of accepted work items that can wait in the mailbox.
    /// </summary>
    public int Capacity { get; }
}
