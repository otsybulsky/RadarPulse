namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingQuarantineEvidence
{
    public RadarProcessingQuarantineEvidence(
        int partitionId,
        int shardId,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingPressureScore partitionPressure,
        RadarProcessingPressureBand partitionBand,
        RadarProcessingHotPartitionClassification observedClassification = RadarProcessingHotPartitionClassification.None)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(evaluationSequence);

        if (!Enum.IsDefined(partitionBand))
        {
            throw new ArgumentOutOfRangeException(nameof(partitionBand), partitionBand, "Pressure band is not defined.");
        }

        if (!Enum.IsDefined(observedClassification))
        {
            throw new ArgumentOutOfRangeException(
                nameof(observedClassification),
                observedClassification,
                "Observed classification is not defined.");
        }

        PartitionId = partitionId;
        ShardId = shardId;
        EvaluationSequence = evaluationSequence;
        TopologyVersion = topologyVersion;
        PartitionPressure = partitionPressure;
        PartitionBand = partitionBand;
        ObservedClassification = observedClassification;
    }

    public int PartitionId { get; }

    public int ShardId { get; }

    public long EvaluationSequence { get; }

    public RadarProcessingTopologyVersion TopologyVersion { get; }

    public RadarProcessingPressureScore PartitionPressure { get; }

    public RadarProcessingPressureBand PartitionBand { get; }

    public RadarProcessingHotPartitionClassification ObservedClassification { get; }
}
