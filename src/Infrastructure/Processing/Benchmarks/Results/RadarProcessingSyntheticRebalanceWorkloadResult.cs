using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingSyntheticRebalanceWorkloadResult
{
    private readonly IReadOnlyList<RadarProcessingRebalanceSessionResult> steps;
    private readonly IReadOnlyList<RadarProcessingRebalanceSkippedReason> skippedReasons;
    private readonly IReadOnlyList<RadarProcessingQuarantineLifecycleState> finalQuarantineStates;

    public RadarProcessingSyntheticRebalanceWorkloadResult(
        RadarProcessingSyntheticRebalanceWorkloadKind kind,
        int sourceCount,
        int partitionCount,
        int shardCount,
        RadarProcessingTopologyVersion initialTopologyVersion,
        RadarProcessingTopologyVersion finalTopologyVersion,
        IReadOnlyList<RadarProcessingRebalanceSessionResult> steps,
        IReadOnlyList<RadarProcessingQuarantineLifecycleState> finalQuarantineStates)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(finalQuarantineStates);

        Kind = kind;
        SourceCount = sourceCount;
        PartitionCount = partitionCount;
        ShardCount = shardCount;
        InitialTopologyVersion = initialTopologyVersion;
        FinalTopologyVersion = finalTopologyVersion;
        this.steps = Array.AsReadOnly(steps.ToArray());
        skippedReasons = Array.AsReadOnly(CollectSkippedReasons(steps));
        this.finalQuarantineStates = Array.AsReadOnly(finalQuarantineStates.ToArray());
    }

    public RadarProcessingSyntheticRebalanceWorkloadKind Kind { get; }

    public int SourceCount { get; }

    public int PartitionCount { get; }

    public int ShardCount { get; }

    public RadarProcessingTopologyVersion InitialTopologyVersion { get; }

    public RadarProcessingTopologyVersion FinalTopologyVersion { get; }

    public IReadOnlyList<RadarProcessingRebalanceSessionResult> Steps => steps;

    public IReadOnlyList<RadarProcessingRebalanceSkippedReason> SkippedReasons => skippedReasons;

    public IReadOnlyList<RadarProcessingQuarantineLifecycleState> FinalQuarantineStates =>
        finalQuarantineStates;

    public int BatchCount => steps.Count;

    public int AcceptedMoveCount => steps.Count(static step => step.PublishedMigration);

    public int DirectHotReliefMoveCount => steps.Count(
        static step => step.DirectHotReliefDecision?.HasAcceptedMove == true);

    public int ColdEvacuationMoveCount => steps.Count(
        static step => step.ColdEvacuationDecision?.HasAcceptedMove == true);

    public long TopologyVersionCount => FinalTopologyVersion.Value - InitialTopologyVersion.Value + 1;

    public bool ValidationSucceeded => steps.All(static step => step.Validation.IsValid);

    public RadarProcessingRebalanceTelemetrySummary FinalTelemetrySummary =>
        steps.Count == 0
            ? RadarProcessingRebalanceTelemetrySummary.Empty
            : steps[^1].TelemetrySummary;

    public long QuarantineEntryCount => FinalTelemetrySummary.Counters.QuarantineEntryCount;

    public long QuarantineClearCount => FinalTelemetrySummary.Counters.QuarantineClearCount;

    public long QuarantineRetryCount => FinalTelemetrySummary.Counters.QuarantineRetryCount;

    public long QuarantineReentryCount => FinalTelemetrySummary.Counters.QuarantineReentryCount;

    public RadarProcessingRebalanceRetentionStats RetentionStats =>
        FinalTelemetrySummary.RetentionStats;

    public bool HasSkippedReason(RadarProcessingRebalanceSkippedReason reason) =>
        skippedReasons.Contains(reason);

    public long CountSkippedReason(RadarProcessingRebalanceSkippedReason reason)
    {
        foreach (var counter in FinalTelemetrySummary.SkippedReasonCounters)
        {
            if (counter.Reason == reason)
            {
                return counter.Count;
            }
        }

        return 0;
    }

    public bool HasQuarantineTransition(RadarProcessingQuarantineTransitionReason reason) =>
        CountQuarantineTransitions(reason) > 0;

    public int CountQuarantineTransitions(RadarProcessingQuarantineTransitionReason reason) =>
        steps.Sum(step => step.QuarantineTransitions.Count(transition => transition.Reason == reason));

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
