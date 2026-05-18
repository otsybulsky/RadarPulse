namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRebalanceRecentDecision
{
    private readonly IReadOnlyList<RadarProcessingRebalanceSkippedReason> skippedReasons;
    private readonly IReadOnlyList<RadarProcessingRebalancePolicyRejection> policyRejections;

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

    public long DecisionId { get; }

    public long EvaluationSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingRebalanceDecisionKind Kind { get; }

    public RadarProcessingRebalanceMoveKind MoveKind { get; }

    public int? PartitionId { get; }

    public int? SourceShardId { get; }

    public int? TargetShardId { get; }

    public IReadOnlyList<RadarProcessingRebalanceSkippedReason> SkippedReasons =>
        skippedReasons;

    public IReadOnlyList<RadarProcessingRebalancePolicyRejection> PolicyRejections =>
        policyRejections;

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
