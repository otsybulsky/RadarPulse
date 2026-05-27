using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Step-by-step result from running a synthetic rebalance workload once.
/// </summary>
public sealed class RadarProcessingSyntheticRebalanceWorkloadResult
{
    private readonly IReadOnlyList<RadarProcessingRebalanceSessionResult> steps;
    private readonly IReadOnlyList<RadarProcessingRebalanceSkippedReason> skippedReasons;
    private readonly IReadOnlyList<RadarProcessingQuarantineLifecycleState> finalQuarantineStates;

    /// <summary>
    /// Creates a workload result from ordered rebalance session steps.
    /// </summary>
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

    /// <summary>
    /// Scenario represented by the workload.
    /// </summary>
    public RadarProcessingSyntheticRebalanceWorkloadKind Kind { get; }

    /// <summary>
    /// Source count in the workload.
    /// </summary>
    public int SourceCount { get; }

    /// <summary>
    /// Partition count in the workload.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Shard count in the workload.
    /// </summary>
    public int ShardCount { get; }

    /// <summary>
    /// Topology version before processing the first batch.
    /// </summary>
    public RadarProcessingTopologyVersion InitialTopologyVersion { get; }

    /// <summary>
    /// Topology version after processing the last batch.
    /// </summary>
    public RadarProcessingTopologyVersion FinalTopologyVersion { get; }

    /// <summary>
    /// Ordered rebalance results for each processed batch.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalanceSessionResult> Steps => steps;

    /// <summary>
    /// Distinct skipped rebalance reasons observed in the run.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalanceSkippedReason> SkippedReasons => skippedReasons;

    /// <summary>
    /// Final quarantine lifecycle states after the run.
    /// </summary>
    public IReadOnlyList<RadarProcessingQuarantineLifecycleState> FinalQuarantineStates =>
        finalQuarantineStates;

    /// <summary>
    /// Number of processed batches.
    /// </summary>
    public int BatchCount => steps.Count;

    /// <summary>
    /// Number of accepted rebalance moves.
    /// </summary>
    public int AcceptedMoveCount => steps.Count(static step => step.PublishedMigration);

    /// <summary>
    /// Number of direct hot-relief moves.
    /// </summary>
    public int DirectHotReliefMoveCount => steps.Count(
        static step => step.DirectHotReliefDecision?.HasAcceptedMove == true);

    /// <summary>
    /// Number of cold evacuation moves.
    /// </summary>
    public int ColdEvacuationMoveCount => steps.Count(
        static step => step.ColdEvacuationDecision?.HasAcceptedMove == true);

    /// <summary>
    /// Number of topology versions observed, including initial and final.
    /// </summary>
    public long TopologyVersionCount => FinalTopologyVersion.Value - InitialTopologyVersion.Value + 1;

    /// <summary>
    /// Indicates whether every step passed rebalance validation.
    /// </summary>
    public bool ValidationSucceeded => steps.All(static step => step.Validation.IsValid);

    /// <summary>
    /// Final rebalance telemetry summary.
    /// </summary>
    public RadarProcessingRebalanceTelemetrySummary FinalTelemetrySummary =>
        steps.Count == 0
            ? RadarProcessingRebalanceTelemetrySummary.Empty
            : steps[^1].TelemetrySummary;

    /// <summary>
    /// Quarantine entry count from final telemetry.
    /// </summary>
    public long QuarantineEntryCount => FinalTelemetrySummary.Counters.QuarantineEntryCount;

    /// <summary>
    /// Quarantine clear count from final telemetry.
    /// </summary>
    public long QuarantineClearCount => FinalTelemetrySummary.Counters.QuarantineClearCount;

    /// <summary>
    /// Quarantine retry count from final telemetry.
    /// </summary>
    public long QuarantineRetryCount => FinalTelemetrySummary.Counters.QuarantineRetryCount;

    /// <summary>
    /// Quarantine reentry count from final telemetry.
    /// </summary>
    public long QuarantineReentryCount => FinalTelemetrySummary.Counters.QuarantineReentryCount;

    /// <summary>
    /// Final telemetry retention statistics.
    /// </summary>
    public RadarProcessingRebalanceRetentionStats RetentionStats =>
        FinalTelemetrySummary.RetentionStats;

    /// <summary>
    /// Indicates whether a skipped rebalance reason was observed.
    /// </summary>
    public bool HasSkippedReason(RadarProcessingRebalanceSkippedReason reason) =>
        skippedReasons.Contains(reason);

    /// <summary>
    /// Counts skipped rebalance decisions for a reason using final telemetry.
    /// </summary>
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

    /// <summary>
    /// Indicates whether a quarantine transition reason was observed.
    /// </summary>
    public bool HasQuarantineTransition(RadarProcessingQuarantineTransitionReason reason) =>
        CountQuarantineTransitions(reason) > 0;

    /// <summary>
    /// Counts quarantine transitions for the supplied reason.
    /// </summary>
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
