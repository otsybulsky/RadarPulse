namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingQuarantineTransition
{
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

    public int PartitionId { get; }

    public int ShardId { get; }

    public long EvaluationSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingQuarantineEffectiveClassification PreviousClassification { get; }

    public RadarProcessingQuarantineEffectiveClassification CurrentClassification { get; }

    public RadarProcessingQuarantineTransitionReason Reason { get; }

    public RadarProcessingPressureScore Pressure { get; }

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
