namespace RadarPulse.Domain.Processing;

/// <summary>
/// Current hot-partition classification and ineffective move evidence.
/// </summary>
public readonly record struct RadarProcessingHotPartitionState
{
    /// <summary>
    /// Creates a hot-partition state snapshot.
    /// </summary>
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

    /// <summary>
    /// Partition represented by the state.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Shard associated with the latest classification evidence.
    /// </summary>
    public int ShardId { get; }

    /// <summary>
    /// Current hot-partition classification.
    /// </summary>
    public RadarProcessingHotPartitionClassification Classification { get; }

    /// <summary>
    /// Evaluation sequence that produced the latest classification.
    /// </summary>
    public long EvaluationSequence { get; }

    /// <summary>
    /// Consecutive ineffective move count used to enter quarantine.
    /// </summary>
    public int IneffectiveMoveCount { get; }

    /// <summary>
    /// Indicates whether direct movement is allowed.
    /// </summary>
    public bool IsMovableHot => Classification == RadarProcessingHotPartitionClassification.MovableHot;

    /// <summary>
    /// Indicates whether the partition is considered intrinsically hot.
    /// </summary>
    public bool IsIntrinsicHot => Classification == RadarProcessingHotPartitionClassification.IntrinsicHot;

    /// <summary>
    /// Indicates whether the partition is quarantined.
    /// </summary>
    public bool IsQuarantined => Classification == RadarProcessingHotPartitionClassification.Quarantined;

    /// <summary>
    /// Indicates whether direct hot-relief planning should skip the partition.
    /// </summary>
    public bool BlocksDirectMove => IsIntrinsicHot || IsQuarantined;
}
