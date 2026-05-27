namespace RadarPulse.Domain.Processing;

/// <summary>
/// Compatibility posture for durable adapter storage.
/// </summary>
public enum RadarProcessingDurableAdapterCompatibilityStatus
{
    /// <summary>
    /// Storage exists and matches the adapter schema.
    /// </summary>
    Compatible = 1,

    /// <summary>
    /// Storage is empty and can be initialized by the adapter.
    /// </summary>
    Empty = 2,

    /// <summary>
    /// Storage exists but is not compatible with the current adapter schema.
    /// </summary>
    Incompatible = 3,

    /// <summary>
    /// Storage compatibility could not be determined because inspection failed.
    /// </summary>
    Failed = 4
}
