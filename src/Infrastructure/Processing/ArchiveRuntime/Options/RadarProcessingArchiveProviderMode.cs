namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Archive-to-processing provider mode used by runtime and benchmark adapters.
/// </summary>
public enum RadarProcessingArchiveProviderMode
{
    /// <summary>
    /// Blocking provider that borrows archive batch lifetime.
    /// </summary>
    BlockingBorrowed = 1,

    /// <summary>
    /// Queued provider that retains owned payload snapshots.
    /// </summary>
    QueuedOwned = 2
}
