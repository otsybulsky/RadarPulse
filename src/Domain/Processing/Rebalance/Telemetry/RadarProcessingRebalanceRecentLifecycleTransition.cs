namespace RadarPulse.Domain.Processing;

/// <summary>
/// Retained compact detail for a recent quarantine lifecycle transition.
/// </summary>
public sealed record RadarProcessingRebalanceRecentLifecycleTransition
{
    /// <summary>
    /// Creates a retained lifecycle transition detail entry.
    /// </summary>
    public RadarProcessingRebalanceRecentLifecycleTransition(
        int partitionId,
        int shardId,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingQuarantineEffectiveClassification previousClassification,
        RadarProcessingQuarantineEffectiveClassification currentClassification,
        RadarProcessingQuarantineTransitionReason reason,
        RadarProcessingPressureScore pressure,
        long quarantineAgeEvaluations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(evaluationSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(quarantineAgeEvaluations);
        RadarProcessingQuarantineTransition.EnsureKnownClassification(
            previousClassification,
            nameof(previousClassification));
        RadarProcessingQuarantineTransition.EnsureKnownClassification(
            currentClassification,
            nameof(currentClassification));

        if (!Enum.IsDefined(reason) || reason == RadarProcessingQuarantineTransitionReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Transition reason must be explicit.");
        }

        PartitionId = partitionId;
        ShardId = shardId;
        EvaluationSequence = evaluationSequence;
        TopologyVersion = topologyVersion;
        PreviousClassification = previousClassification;
        CurrentClassification = currentClassification;
        Reason = reason;
        Pressure = pressure;
        QuarantineAgeEvaluations = quarantineAgeEvaluations;
    }

    /// <summary>
    /// Partition whose lifecycle changed.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Shard that owned the partition at the transition.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Policy evaluation sequence for the transition.
    /// </summary>
    public long EvaluationSequence { get; }

    /// <summary>
    /// Topology version associated with the transition evidence.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Effective classification before the transition.
    /// </summary>
    public RadarProcessingQuarantineEffectiveClassification PreviousClassification { get; }

    /// <summary>
    /// Effective classification after the transition.
    /// </summary>
    public RadarProcessingQuarantineEffectiveClassification CurrentClassification { get; }

    /// <summary>
    /// Reason for the lifecycle transition.
    /// </summary>
    public RadarProcessingQuarantineTransitionReason Reason { get; }

    /// <summary>
    /// Pressure evidence associated with the transition.
    /// </summary>
    public RadarProcessingPressureScore Pressure { get; }

    /// <summary>
    /// Current quarantine age in evaluations.
    /// </summary>
    public long QuarantineAgeEvaluations { get; }

    /// <summary>
    /// Creates retained transition detail from a lifecycle transition.
    /// </summary>
    public static RadarProcessingRebalanceRecentLifecycleTransition FromTransition(
        RadarProcessingQuarantineTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        return new RadarProcessingRebalanceRecentLifecycleTransition(
            transition.PartitionId,
            transition.ShardId,
            transition.EvaluationSequence,
            transition.TopologyVersion,
            transition.PreviousClassification,
            transition.CurrentClassification,
            transition.Reason,
            transition.Pressure,
            transition.QuarantineAgeEvaluations);
    }
}
