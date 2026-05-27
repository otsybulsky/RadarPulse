namespace RadarPulse.Domain.Processing;

/// <summary>
/// Immutable planner decision for one rebalance evaluation.
/// </summary>
/// <remarks>
/// Decisions preserve the topology version, pressure-window sample count,
/// optional candidate, and explicit skipped or policy reasons. Accepted decisions
/// are the only decisions eligible for migration publication.
/// </remarks>
public sealed class RadarProcessingRebalanceDecision
{
    private readonly IReadOnlyList<RadarProcessingRebalanceSkippedReason> skippedReasons;
    private readonly IReadOnlyList<RadarProcessingRebalancePolicyRejection> policyRejections;

    private RadarProcessingRebalanceDecision(
        long decisionId,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingTopologyVersion? resultTopologyVersion,
        int pressureWindowSampleCount,
        RadarProcessingRebalanceDecisionKind kind,
        RadarProcessingRebalanceCandidate? candidate,
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReason> skippedReasons,
        IReadOnlyCollection<RadarProcessingRebalancePolicyRejection> policyRejections)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decisionId);
        ArgumentOutOfRangeException.ThrowIfNegative(evaluationSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(pressureWindowSampleCount);
        ArgumentNullException.ThrowIfNull(skippedReasons);
        ArgumentNullException.ThrowIfNull(policyRejections);

        DecisionId = decisionId;
        EvaluationSequence = evaluationSequence;
        TopologyVersion = topologyVersion;
        ResultTopologyVersion = resultTopologyVersion;
        PressureWindowSampleCount = pressureWindowSampleCount;
        Kind = kind;
        Candidate = candidate;
        this.skippedReasons = CopySkippedReasons(skippedReasons);
        this.policyRejections = CopyPolicyRejections(policyRejections);
    }

    /// <summary>
    /// Monotonic decision id within the rebalance session.
    /// </summary>
    public long DecisionId { get; }

    /// <summary>
    /// Rebalance policy evaluation sequence used by the planner.
    /// </summary>
    public long EvaluationSequence { get; }

    /// <summary>
    /// Topology version evaluated by the planner.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Published topology version after an accepted move, when known.
    /// </summary>
    public RadarProcessingTopologyVersion? ResultTopologyVersion { get; }

    /// <summary>
    /// Number of pressure samples available to the planner.
    /// </summary>
    public int PressureWindowSampleCount { get; }

    /// <summary>
    /// Decision outcome category.
    /// </summary>
    public RadarProcessingRebalanceDecisionKind Kind { get; }

    /// <summary>
    /// Candidate associated with accepted or rejected-candidate decisions.
    /// </summary>
    public RadarProcessingRebalanceCandidate? Candidate { get; }

    /// <summary>
    /// Candidate move kind, or none when no candidate exists.
    /// </summary>
    public RadarProcessingRebalanceMoveKind MoveKind =>
        Candidate?.MoveKind ?? RadarProcessingRebalanceMoveKind.None;

    /// <summary>
    /// Candidate partition id, when a candidate exists.
    /// </summary>
    public int? PartitionId => Candidate?.PartitionId;

    /// <summary>
    /// Candidate source shard id, when a candidate exists.
    /// </summary>
    public int? SourceShardId => Candidate?.SourceShardId;

    /// <summary>
    /// Candidate target shard id, when a candidate exists.
    /// </summary>
    public int? TargetShardId => Candidate?.TargetShardId;

    /// <summary>
    /// Candidate projected pressure, or zero when no candidate exists.
    /// </summary>
    public RadarProcessingProjectedPressure ProjectedPressure =>
        Candidate?.ProjectedPressure ?? RadarProcessingProjectedPressure.Zero;

    /// <summary>
    /// Candidate expected relief, or zero when no candidate exists.
    /// </summary>
    public double ExpectedRelief => Candidate?.ExpectedRelief ?? 0.0;

    /// <summary>
    /// Planner-level reasons that explain no-action or rejected-candidate outcomes.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalanceSkippedReason> SkippedReasons =>
        skippedReasons;

    /// <summary>
    /// Policy-level rejections that blocked a candidate.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalancePolicyRejection> PolicyRejections =>
        policyRejections;

    /// <summary>
    /// Indicates whether the decision can be published as a migration.
    /// </summary>
    public bool HasAcceptedMove => Kind == RadarProcessingRebalanceDecisionKind.AcceptedMove;

    /// <summary>
    /// Creates a decision indicating that no candidate should be attempted.
    /// </summary>
    public static RadarProcessingRebalanceDecision NoAction(
        long decisionId,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        int pressureWindowSampleCount,
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReason> skippedReasons)
    {
        ArgumentNullException.ThrowIfNull(skippedReasons);

        if (skippedReasons.Count == 0)
        {
            throw new ArgumentException("No-action rebalance decisions must include at least one reason.", nameof(skippedReasons));
        }

        return new RadarProcessingRebalanceDecision(
            decisionId,
            evaluationSequence,
            topologyVersion,
            resultTopologyVersion: null,
            pressureWindowSampleCount,
            RadarProcessingRebalanceDecisionKind.NoAction,
            candidate: null,
            skippedReasons,
            Array.Empty<RadarProcessingRebalancePolicyRejection>());
    }

    /// <summary>
    /// Creates a decision for a candidate accepted by the planner and policy.
    /// </summary>
    public static RadarProcessingRebalanceDecision AcceptedMove(
        long decisionId,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        int pressureWindowSampleCount,
        RadarProcessingRebalanceCandidate candidate,
        RadarProcessingTopologyVersion? resultTopologyVersion = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new RadarProcessingRebalanceDecision(
            decisionId,
            evaluationSequence,
            topologyVersion,
            resultTopologyVersion,
            pressureWindowSampleCount,
            RadarProcessingRebalanceDecisionKind.AcceptedMove,
            candidate,
            Array.Empty<RadarProcessingRebalanceSkippedReason>(),
            Array.Empty<RadarProcessingRebalancePolicyRejection>());
    }

    /// <summary>
    /// Creates a rejected-candidate decision from a policy rejection result.
    /// </summary>
    public static RadarProcessingRebalanceDecision RejectedCandidate(
        long decisionId,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        int pressureWindowSampleCount,
        RadarProcessingRebalanceCandidate candidate,
        RadarProcessingRebalancePolicyResult policyResult)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policyResult);

        if (policyResult.IsAllowed)
        {
            throw new ArgumentException("Rejected candidate decisions require a rejected policy result.", nameof(policyResult));
        }

        if (policyResult.Input != candidate.ToPolicyInput())
        {
            throw new ArgumentException("Policy result input must match the rejected candidate.", nameof(policyResult));
        }

        return RejectedCandidate(
            decisionId,
            evaluationSequence,
            topologyVersion,
            pressureWindowSampleCount,
            candidate,
            MapPolicyRejections(policyResult.Rejections),
            policyResult.Rejections);
    }

    /// <summary>
    /// Creates a rejected-candidate decision from explicit planner and policy reasons.
    /// </summary>
    public static RadarProcessingRebalanceDecision RejectedCandidate(
        long decisionId,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        int pressureWindowSampleCount,
        RadarProcessingRebalanceCandidate candidate,
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReason> skippedReasons,
        IReadOnlyCollection<RadarProcessingRebalancePolicyRejection>? policyRejections = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(skippedReasons);

        policyRejections ??= Array.Empty<RadarProcessingRebalancePolicyRejection>();

        if (skippedReasons.Count == 0 && policyRejections.Count == 0)
        {
            throw new ArgumentException(
                "Rejected candidate decisions must include at least one skipped reason or policy rejection.",
                nameof(skippedReasons));
        }

        return new RadarProcessingRebalanceDecision(
            decisionId,
            evaluationSequence,
            topologyVersion,
            resultTopologyVersion: null,
            pressureWindowSampleCount,
            RadarProcessingRebalanceDecisionKind.RejectedCandidate,
            candidate,
            skippedReasons,
            policyRejections);
    }

    private static IReadOnlyList<RadarProcessingRebalanceSkippedReason> CopySkippedReasons(
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReason> reasons) =>
        CopyDistinct(
            reasons,
            RadarProcessingRebalanceSkippedReason.None,
            nameof(reasons));

    private static IReadOnlyList<RadarProcessingRebalancePolicyRejection> CopyPolicyRejections(
        IReadOnlyCollection<RadarProcessingRebalancePolicyRejection> rejections) =>
        CopyDistinct(
            rejections,
            RadarProcessingRebalancePolicyRejection.None,
            nameof(rejections));

    private static IReadOnlyList<T> CopyDistinct<T>(
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
                throw new ArgumentOutOfRangeException(paramName, value, "Reason values must be explicit.");
            }

            if (seen.Add(value))
            {
                result.Add(value);
            }
        }

        return Array.AsReadOnly(result.ToArray());
    }

    private static RadarProcessingRebalanceSkippedReason[] MapPolicyRejections(
        IReadOnlyCollection<RadarProcessingRebalancePolicyRejection> rejections)
    {
        var result = new List<RadarProcessingRebalanceSkippedReason>(rejections.Count);

        foreach (var rejection in rejections)
        {
            result.Add(rejection switch
            {
                RadarProcessingRebalancePolicyRejection.PartitionBelowMinimumResidency =>
                    RadarProcessingRebalanceSkippedReason.CandidatePartitionBelowMinimumResidency,
                RadarProcessingRebalancePolicyRejection.PartitionInCooldown =>
                    RadarProcessingRebalanceSkippedReason.CandidatePartitionInCooldown,
                RadarProcessingRebalancePolicyRejection.SourceShardInCooldown =>
                    RadarProcessingRebalanceSkippedReason.SourceShardInCooldown,
                RadarProcessingRebalancePolicyRejection.TargetShardInCooldown =>
                    RadarProcessingRebalanceSkippedReason.TargetShardInCooldown,
                RadarProcessingRebalancePolicyRejection.GlobalMoveBudgetExhausted =>
                    RadarProcessingRebalanceSkippedReason.GlobalMoveBudgetExhausted,
                RadarProcessingRebalancePolicyRejection.SourceShardMoveBudgetExhausted =>
                    RadarProcessingRebalanceSkippedReason.SourceShardMoveBudgetExhausted,
                RadarProcessingRebalancePolicyRejection.TargetShardReceiveBudgetExhausted =>
                    RadarProcessingRebalanceSkippedReason.TargetShardReceiveBudgetExhausted,
                RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit =>
                    RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit,
                RadarProcessingRebalancePolicyRejection.TargetHeadroomExceeded =>
                    RadarProcessingRebalanceSkippedReason.TargetHeadroomExceeded,
                _ => throw new ArgumentOutOfRangeException(nameof(rejections), rejection, "Unsupported policy rejection.")
            });
        }

        return result.ToArray();
    }
}
