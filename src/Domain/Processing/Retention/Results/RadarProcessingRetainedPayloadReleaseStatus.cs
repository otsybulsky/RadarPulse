namespace RadarPulse.Domain.Processing;

/// <summary>
/// Outcome of releasing a retained payload resource.
/// </summary>
public enum RadarProcessingRetainedPayloadReleaseStatus
{
    /// <summary>
    /// The retained resource was released.
    /// </summary>
    Released = 1,

    /// <summary>
    /// The resource was already released before this request.
    /// </summary>
    AlreadyReleased = 2,

    /// <summary>
    /// The release callback failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The strategy did not require an explicit release.
    /// </summary>
    NotRequired = 4
}
