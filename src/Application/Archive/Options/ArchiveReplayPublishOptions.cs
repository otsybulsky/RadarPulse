namespace RadarPulse.Application.Archive;

/// <summary>
/// Options for replaying Archive II gate-moment events from a file.
/// </summary>
/// <param name="DegreeOfParallelism">Number of compressed records that can be processed in parallel.</param>
public sealed record ArchiveReplayPublishOptions(int DegreeOfParallelism)
{
    /// <summary>
    /// Sequential replay mode that preserves the single-worker projection path.
    /// </summary>
    public static ArchiveReplayPublishOptions Sequential { get; } = new(1);
}
