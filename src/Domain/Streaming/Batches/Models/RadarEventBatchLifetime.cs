namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Ownership lifetime of a radar event batch's metadata and payload buffers.
/// </summary>
public enum RadarEventBatchLifetime : byte
{
    /// <summary>
    /// Batch owns stable buffers that may outlive the producer.
    /// </summary>
    Owned = 0,

    /// <summary>
    /// Batch borrows producer buffers and must be consumed before producer reset.
    /// </summary>
    Leased = 1
}
