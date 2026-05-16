namespace RadarPulse.Domain.Processing;

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

    public long DecisionId { get; }

    public long EvaluationSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingTopologyVersion? ResultTopologyVersion { get; }

    public int PressureWindowSampleCount { get; }

    public RadarProcessingRebalanceDecisionKind Kind { get; }

    public RadarProcessingRebalanceCandidate? Candidate { get; }

    public RadarProcessingRebalanceMoveKind MoveKind =>
        Candidate?.MoveKind ?? RadarProcessingRebalanceMoveKind.None;

    public int? PartitionId => Candidate?.PartitionId;

    public int? SourceShardId => Candidate?.SourceShardId;

    public int? TargetShardId => Candidate?.TargetShardId;

    public RadarProcessingProjectedPressure ProjectedPressure =>
        Candidate?.ProjectedPressure ?? RadarProcessingProjectedPressure.Zero;

    public double ExpectedRelief => Candidate?.ExpectedRelief ?? 0.0;

    public IReadOnlyList<RadarProcessingRebalanceSkippedReason> SkippedReasons =>
        skippedReasons;

    public IReadOnlyList<RadarProcessingRebalancePolicyRejection> PolicyRejections =>
        policyRejections;

    public bool HasAcceptedMove => Kind == RadarProcessingRebalanceDecisionKind.AcceptedMove;

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
        IReadOnlyCollection<RadarProcessingRebalanceSkippedReason> reasons)
    {
        var copy = CopyDistinct(reasons, RadarProcessingRebalanceSkippedReason.None, nameof(reasons));
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<RadarProcessingRebalancePolicyRejection> CopyPolicyRejections(
        IReadOnlyCollection<RadarProcessingRebalancePolicyRejection> rejections)
    {
        var copy = CopyDistinct(rejections, RadarProcessingRebalancePolicyRejection.None, nameof(rejections));
        return Array.AsReadOnly(copy);
    }

    private static T[] CopyDistinct<T>(
        IEnumerable<T> values,
        T disallowedValue,
        string paramName)
        where T : struct, Enum
    {
        var result = new List<T>();
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

        return result.ToArray();
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
