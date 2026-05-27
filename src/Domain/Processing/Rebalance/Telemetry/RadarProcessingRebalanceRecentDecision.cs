namespace RadarPulse.Domain.Processing;

/// <summary>
/// Retained compact detail for a recent rebalance decision.
/// </summary>
public sealed class RadarProcessingRebalanceRecentDecision
{
    private readonly IReadOnlyList<RadarProcessingRebalanceSkippedReason> skippedReasons;
    private readonly IReadOnlyList<RadarProcessingRebalancePolicyRejection> policyRejections;

    /// <summary>
    /// Creates a retained decision detail entry.
    /// </summary>
    public RadarProcessingRebalanceRecentDecision(
        long decisionId,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingRebalanceDecisionKind kind,
        RadarProcessingRebalanceMoveKind moveKind = RadarProcessingRebalanceMoveKind.None,
        int? partitionId = null,
        int? sourceShardId = null,
        int? targetShardId = null,
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReason>? skippedReasons = null,
        IReadOnlyCollection<RadarProcessingRebalancePolicyRejection>? policyRejections = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decisionId);
        ArgumentOutOfRangeException.ThrowIfNegative(evaluationSequence);
        EnsureKnownDecisionKind(kind);
        EnsureKnownMoveKind(moveKind);
        ThrowIfNegative(partitionId, nameof(partitionId));
        ThrowIfNegative(sourceShardId, nameof(sourceShardId));
        ThrowIfNegative(targetShardId, nameof(targetShardId));

        DecisionId = decisionId;
        EvaluationSequence = evaluationSequence;
        TopologyVersion = topologyVersion;
        Kind = kind;
        MoveKind = moveKind;
        PartitionId = partitionId;
        SourceShardId = sourceShardId;
        TargetShardId = targetShardId;
        this.skippedReasons = CopyExplicitValues(
            skippedReasons ?? Array.Empty<RadarProcessingRebalanceSkippedReason>(),
            RadarProcessingRebalanceSkippedReason.None,
            nameof(skippedReasons));
        this.policyRejections = CopyExplicitValues(
            policyRejections ?? Array.Empty<RadarProcessingRebalancePolicyRejection>(),
            RadarProcessingRebalancePolicyRejection.None,
            nameof(policyRejections));
    }

    /// <summary>
    /// Decision id retained from the source decision.
    /// </summary>
    public long DecisionId { get; }

    /// <summary>
    /// Policy evaluation sequence for the decision.
    /// </summary>
    public long EvaluationSequence { get; }

    /// <summary>
    /// Topology version evaluated by the decision.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Decision outcome category.
    /// </summary>
    public RadarProcessingRebalanceDecisionKind Kind { get; }

    /// <summary>
    /// Candidate move kind, or none when no candidate existed.
    /// </summary>
    public RadarProcessingRebalanceMoveKind MoveKind { get; }

    /// <summary>
    /// Candidate partition id, when available.
    /// </summary>
    public int? PartitionId { get; }

    /// <summary>
    /// Candidate source shard id, when available.
    /// </summary>
    public int? SourceShardId { get; }

    /// <summary>
    /// Candidate target shard id, when available.
    /// </summary>
    public int? TargetShardId { get; }

    /// <summary>
    /// Planner skipped reasons retained for the decision.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalanceSkippedReason> SkippedReasons =>
        skippedReasons;

    /// <summary>
    /// Policy rejections retained for the decision.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalancePolicyRejection> PolicyRejections =>
        policyRejections;

    /// <summary>
    /// Creates retained decision detail from a full decision.
    /// </summary>
    public static RadarProcessingRebalanceRecentDecision FromDecision(
        RadarProcessingRebalanceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new RadarProcessingRebalanceRecentDecision(
            decision.DecisionId,
            decision.EvaluationSequence,
            decision.TopologyVersion,
            decision.Kind,
            decision.MoveKind,
            decision.PartitionId,
            decision.SourceShardId,
            decision.TargetShardId,
            decision.SkippedReasons,
            decision.PolicyRejections);
    }

    internal static void EnsureKnownDecisionKind(
        RadarProcessingRebalanceDecisionKind kind)
    {
        if (kind is not RadarProcessingRebalanceDecisionKind.NoAction and
            not RadarProcessingRebalanceDecisionKind.AcceptedMove and
            not RadarProcessingRebalanceDecisionKind.RejectedCandidate)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    internal static void EnsureKnownMoveKind(
        RadarProcessingRebalanceMoveKind moveKind)
    {
        if (moveKind is not RadarProcessingRebalanceMoveKind.None and
            not RadarProcessingRebalanceMoveKind.DirectHotRelief and
            not RadarProcessingRebalanceMoveKind.ColdEvacuation and
            not RadarProcessingRebalanceMoveKind.RoomMakingReserved)
        {
            throw new ArgumentOutOfRangeException(nameof(moveKind));
        }
    }

    private static void ThrowIfNegative(
        int? value,
        string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }

    private static IReadOnlyList<T> CopyExplicitValues<T>(
        IReadOnlyCollection<T> values,
        T disallowedValue,
        string paramName)
        where T : struct, Enum
    {
        if (values.Count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new List<T>(values.Count);
        var seen = new HashSet<T>();

        foreach (var value in values)
        {
            if (!Enum.IsDefined(value) || EqualityComparer<T>.Default.Equals(value, disallowedValue))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Values must be explicit.");
            }

            if (seen.Add(value))
            {
                result.Add(value);
            }
        }

        return Array.AsReadOnly(result.ToArray());
    }
}
