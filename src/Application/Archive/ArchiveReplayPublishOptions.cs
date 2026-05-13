namespace RadarPulse.Application.Archive;

public sealed record ArchiveReplayPublishOptions(int DegreeOfParallelism)
{
    public static ArchiveReplayPublishOptions Sequential { get; } = new(1);
}
