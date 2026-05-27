namespace RadarPulse.Domain.Processing;

/// <summary>
/// Immutable event describing a quarantine lifecycle transition.
/// </summary>
public sealed record RadarProcessingQuarantineTransition
{
    /// <summary>
    /// Creates a quarantine lifecycle transition.
    /// </summary>
    public RadarProcessingQuarantineTransition(
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
        EnsureKnownClassification(previousClassification, nameof(previousClassification));
        EnsureKnownClassification(currentClassification, nameof(currentClassification));

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
    /// Owner shard at transition time.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Evaluation sequence that produced the transition.
    /// </summary>
    public long EvaluationSequence { get; }

    /// <summary>
    /// Topology version associated with transition evidence.
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
    /// Reason for the transition.
    /// </summary>
    public RadarProcessingQuarantineTransitionReason Reason { get; }

    /// <summary>
    /// Pressure score that drove the transition.
    /// </summary>
    public RadarProcessingPressureScore Pressure { get; }

    /// <summary>
    /// Quarantine age in evaluations at transition time.
    /// </summary>
    public long QuarantineAgeEvaluations { get; }

    internal static void EnsureKnownClassification(
        RadarProcessingQuarantineEffectiveClassification classification,
        string paramName)
    {
        if (!Enum.IsDefined(classification))
        {
            throw new ArgumentOutOfRangeException(paramName, classification, "Classification is not defined.");
        }
    }
}
