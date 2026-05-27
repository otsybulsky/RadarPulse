namespace RadarPulse.Domain.Processing;

/// <summary>
/// Behavior used when a provider queue reaches capacity.
/// </summary>
public enum RadarProcessingProviderQueueFullMode
{
    /// <summary>
    /// Return a full result immediately.
    /// </summary>
    ReturnFull = 1,

    /// <summary>
    /// Wait for capacity, optionally bounded by an enqueue timeout.
    /// </summary>
    Wait = 2
}
