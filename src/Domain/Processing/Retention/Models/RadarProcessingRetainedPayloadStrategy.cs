namespace RadarPulse.Domain.Processing;

/// <summary>
/// Strategy used to retain queued batch payload ownership.
/// </summary>
public enum RadarProcessingRetainedPayloadStrategy
{
    /// <summary>
    /// Retain payload by creating an owned snapshot copy.
    /// </summary>
    SnapshotCopy = 1,

    /// <summary>
    /// Retain payload through a pooled copy that must be released.
    /// </summary>
    PooledCopy = 2,

    /// <summary>
    /// Retain payload by transferring builder-owned buffers.
    /// </summary>
    BuilderTransfer = 3
}
