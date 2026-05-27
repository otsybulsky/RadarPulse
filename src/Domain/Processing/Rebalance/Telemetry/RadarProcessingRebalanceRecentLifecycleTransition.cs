namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRebalanceRecentLifecycleTransition
{
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

    public int PartitionId { get; }

    public int ShardId { get; }

    public long EvaluationSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingQuarantineEffectiveClassification PreviousClassification { get; }

    public RadarProcessingQuarantineEffectiveClassification CurrentClassification { get; }

    public RadarProcessingQuarantineTransitionReason Reason { get; }

    public RadarProcessingPressureScore Pressure { get; }

    public long QuarantineAgeEvaluations { get; }

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
