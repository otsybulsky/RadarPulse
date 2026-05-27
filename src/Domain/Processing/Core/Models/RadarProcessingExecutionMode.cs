namespace RadarPulse.Domain.Processing;

/// <summary>
/// Selects the processing engine used to apply a radar event batch.
/// </summary>
public enum RadarProcessingExecutionMode : byte
{
    /// <summary>
    /// Processes events in batch order on the caller thread without partition telemetry.
    /// </summary>
    Sequential = 1,

    /// <summary>
    /// Routes the batch through partition and shard boundaries while preserving a synchronous commit barrier.
    /// </summary>
    PartitionedBarrier = 2,

    /// <summary>
    /// Routes shard work through asynchronous workers and aggregates the shard completions before commit.
    /// </summary>
    AsyncShardTransport = 3
}
