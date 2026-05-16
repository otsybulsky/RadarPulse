namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingHotPartitionState
{
    public RadarProcessingHotPartitionState(
        int partitionId,
        int shardId,
        RadarProcessingHotPartitionClassification classification,
        long evaluationSequence,
        int ineffectiveMoveCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);
        ArgumentOutOfRangeException.ThrowIfNegative(evaluationSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(ineffectiveMoveCount);

        if (!Enum.IsDefined(classification))
        {
            throw new ArgumentOutOfRangeException(nameof(classification), classification, "Classification is not defined.");
        }

        PartitionId = partitionId;
        ShardId = shardId;
        Classification = classification;
        EvaluationSequence = evaluationSequence;
        IneffectiveMoveCount = ineffectiveMoveCount;
    }

    public int PartitionId { get; }

    public int ShardId { get; }

    public RadarProcessingHotPartitionClassification Classification { get; }

    public long EvaluationSequence { get; }

    public int IneffectiveMoveCount { get; }

    public bool IsMovableHot => Classification == RadarProcessingHotPartitionClassification.MovableHot;

    public bool IsIntrinsicHot => Classification == RadarProcessingHotPartitionClassification.IntrinsicHot;

    public bool IsQuarantined => Classification == RadarProcessingHotPartitionClassification.Quarantined;

    public bool BlocksDirectMove => IsIntrinsicHot || IsQuarantined;
}
