namespace RadarPulse.Domain.Processing;

/// <summary>
/// Controls how async shard work is assigned to workers.
/// </summary>
public enum RadarProcessingWorkerAffinity : byte
{
    /// <summary>
    /// Allows dispatch to choose any available worker.
    /// </summary>
    None = 0,

    /// <summary>
    /// Assigns work consistently by shard ownership.
    /// </summary>
    Shard = 1
}
