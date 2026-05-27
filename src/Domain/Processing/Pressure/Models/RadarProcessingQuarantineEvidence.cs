namespace RadarPulse.Domain.Processing;

/// <summary>
/// Pressure and classification evidence submitted to quarantine lifecycle evaluation.
/// </summary>
public sealed record RadarProcessingQuarantineEvidence
{
    /// <summary>
    /// Creates quarantine lifecycle evidence for a partition.
    /// </summary>
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

    /// <summary>
    /// Partition represented by the evidence.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Shard that owns the partition for the evidence sample.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Evaluation sequence associated with the evidence.
    /// </summary>
    public long EvaluationSequence { get; }

    /// <summary>
    /// Topology version associated with the evidence.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion { get; }

    /// <summary>
    /// Partition pressure score.
    /// </summary>
    public RadarProcessingPressureScore PartitionPressure { get; }

    /// <summary>
    /// Partition pressure band.
    /// </summary>
    public RadarProcessingPressureBand PartitionBand { get; }

    /// <summary>
    /// Classification observed before lifecycle policy is applied.
    /// </summary>
    public RadarProcessingHotPartitionClassification ObservedClassification { get; }
}
