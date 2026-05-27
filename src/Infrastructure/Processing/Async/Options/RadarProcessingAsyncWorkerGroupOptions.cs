using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Runtime options for the async worker group used by shard transport.
/// </summary>
public sealed record RadarProcessingAsyncWorkerGroupOptions
{
    /// <summary>
    /// Default worker group settings derived from default async execution options.
    /// </summary>
    public static RadarProcessingAsyncWorkerGroupOptions Default { get; } = new();

    /// <summary>
    /// Creates worker group settings from async execution options.
    /// </summary>
    public RadarProcessingAsyncWorkerGroupOptions(
        RadarProcessingAsyncExecutionOptions? execution = null)
    {
        Execution = execution ?? RadarProcessingAsyncExecutionOptions.Default;
    }

    /// <summary>
    /// Accepted async execution options for worker count, queue capacity, and timeout behavior.
    /// </summary>
    public RadarProcessingAsyncExecutionOptions Execution { get; }

    /// <summary>
    /// Number of worker mailboxes in the group.
    /// </summary>
    public int WorkerCount => Execution.WorkerCount;

    /// <summary>
    /// Per-worker mailbox capacity.
    /// </summary>
    public int QueueCapacity => Execution.QueueCapacity;
}
