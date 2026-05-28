using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayShapeValidator
{
    private sealed record ArchiveTwoReplayShapeAnalysis(
        ArchiveTwoReplayShapeValidationMetrics Metrics,
        ArchiveTwoReplayShapeUnevennessSummary RecordUnevenness,
        ArchiveTwoReplayShapeUnevennessSummary SweepUnevenness,
        ArchiveTwoReplayShapeUnevennessSummary RadialUnevenness,
        ArchiveTwoReplayShapeUnevennessSummary TimeBucketUnevenness);
}
