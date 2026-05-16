using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingSyntheticRebalanceWorkloadResult
{
    private readonly IReadOnlyList<RadarProcessingRebalanceSessionResult> steps;
    private readonly IReadOnlyList<RadarProcessingRebalanceSkippedReason> skippedReasons;

    public RadarProcessingSyntheticRebalanceWorkloadResult(
        RadarProcessingSyntheticRebalanceWorkloadKind kind,
        int sourceCount,
        int partitionCount,
        int shardCount,
        RadarProcessingTopologyVersion initialTopologyVersion,
        RadarProcessingTopologyVersion finalTopologyVersion,
        IReadOnlyList<RadarProcessingRebalanceSessionResult> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        Kind = kind;
        SourceCount = sourceCount;
        PartitionCount = partitionCount;
        ShardCount = shardCount;
        InitialTopologyVersion = initialTopologyVersion;
        FinalTopologyVersion = finalTopologyVersion;
        this.steps = Array.AsReadOnly(steps.ToArray());
        skippedReasons = Array.AsReadOnly(CollectSkippedReasons(steps));
    }

    public RadarProcessingSyntheticRebalanceWorkloadKind Kind { get; }

    public int SourceCount { get; }

    public int PartitionCount { get; }

    public int ShardCount { get; }

    public RadarProcessingTopologyVersion InitialTopologyVersion { get; }

    public RadarProcessingTopologyVersion FinalTopologyVersion { get; }

    public IReadOnlyList<RadarProcessingRebalanceSessionResult> Steps => steps;

    public IReadOnlyList<RadarProcessingRebalanceSkippedReason> SkippedReasons => skippedReasons;

    public int BatchCount => steps.Count;

    public int AcceptedMoveCount => steps.Count(static step => step.PublishedMigration);

    public int DirectHotReliefMoveCount => steps.Count(
        static step => step.DirectHotReliefDecision?.HasAcceptedMove == true);

    public int ColdEvacuationMoveCount => steps.Count(
        static step => step.ColdEvacuationDecision?.HasAcceptedMove == true);

    public long TopologyVersionCount => FinalTopologyVersion.Value - InitialTopologyVersion.Value + 1;

    public bool ValidationSucceeded => steps.All(static step => step.Validation.IsValid);

    public bool HasSkippedReason(RadarProcessingRebalanceSkippedReason reason) =>
        skippedReasons.Contains(reason);

    private static RadarProcessingRebalanceSkippedReason[] CollectSkippedReasons(
        IReadOnlyList<RadarProcessingRebalanceSessionResult> steps)
    {
        var seen = new HashSet<RadarProcessingRebalanceSkippedReason>();
        var result = new List<RadarProcessingRebalanceSkippedReason>();

        foreach (var step in steps)
        {
            AddReasons(step.DirectHotReliefDecision, seen, result);
            AddReasons(step.ColdEvacuationDecision, seen, result);
        }

        return result.ToArray();
    }

    private static void AddReasons(
        RadarProcessingRebalanceDecision? decision,
        HashSet<RadarProcessingRebalanceSkippedReason> seen,
        List<RadarProcessingRebalanceSkippedReason> result)
    {
        if (decision is null)
        {
            return;
        }

        foreach (var reason in decision.SkippedReasons)
        {
            if (seen.Add(reason))
            {
                result.Add(reason);
            }
        }
    }
}
