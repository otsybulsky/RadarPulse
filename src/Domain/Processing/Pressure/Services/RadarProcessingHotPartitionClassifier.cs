namespace RadarPulse.Domain.Processing;

/// <summary>
/// Tracks hot-partition classifications used by rebalance planners.
/// </summary>
/// <remarks>
/// The classifier records whether a hot partition is movable, intrinsically hot,
/// or quarantined. Move outcomes can increase ineffective move counts and push a
/// partition into quarantine after repeated insufficient relief.
/// </remarks>
public sealed class RadarProcessingHotPartitionClassifier
{
    private readonly RadarProcessingHotPartitionState[] partitions;

    /// <summary>
    /// Creates a classifier for a fixed partition count.
    /// </summary>
    public RadarProcessingHotPartitionClassifier(
        int partitionCount,
        int quarantineIneffectiveMoveCount = 2,
        double minimumEffectiveReliefRatio = 0.25)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quarantineIneffectiveMoveCount);

        if (double.IsNaN(minimumEffectiveReliefRatio) ||
            double.IsInfinity(minimumEffectiveReliefRatio) ||
            minimumEffectiveReliefRatio < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumEffectiveReliefRatio),
                minimumEffectiveReliefRatio,
                "Minimum effective relief ratio must be finite and non-negative.");
        }

        PartitionCount = partitionCount;
        QuarantineIneffectiveMoveCount = quarantineIneffectiveMoveCount;
        MinimumEffectiveReliefRatio = minimumEffectiveReliefRatio;
        partitions = new RadarProcessingHotPartitionState[partitionCount];

        for (var partitionId = 0; partitionId < partitions.Length; partitionId++)
        {
            partitions[partitionId] = CreateState(
                partitionId,
                shardId: 0,
                RadarProcessingHotPartitionClassification.None,
                evaluationSequence: 0,
                ineffectiveMoveCount: 0);
        }
    }

    /// <summary>
    /// Number of partitions tracked by the classifier.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Ineffective move count required before quarantine classification.
    /// </summary>
    public int QuarantineIneffectiveMoveCount { get; }

    /// <summary>
    /// Minimum ratio of actual relief to expected relief considered effective.
    /// </summary>
    public double MinimumEffectiveReliefRatio { get; }

    /// <summary>
    /// Returns classification state for a partition.
    /// </summary>
    public RadarProcessingHotPartitionState GetPartition(int partitionId)
    {
        EnsurePartitionId(partitionId);
        return partitions[partitionId];
    }

    /// <summary>
    /// Marks a partition as movable hot.
    /// </summary>
    public RadarProcessingHotPartitionState ClassifyMovableHot(
        int partitionId,
        int shardId,
        long evaluationSequence)
    {
        EnsurePartitionId(partitionId);
        var current = partitions[partitionId];
        return SetState(
            partitionId,
            shardId,
            RadarProcessingHotPartitionClassification.MovableHot,
            evaluationSequence,
            current.IneffectiveMoveCount);
    }

    /// <summary>
    /// Marks a partition as intrinsically hot, blocking direct movement.
    /// </summary>
    public RadarProcessingHotPartitionState ClassifyIntrinsicHot(
        int partitionId,
        int shardId,
        long evaluationSequence)
    {
        EnsurePartitionId(partitionId);
        var current = partitions[partitionId];
        return SetState(
            partitionId,
            shardId,
            RadarProcessingHotPartitionClassification.IntrinsicHot,
            evaluationSequence,
            current.IneffectiveMoveCount);
    }

    /// <summary>
    /// Marks a partition as quarantined and raises ineffective move count to the threshold.
    /// </summary>
    public RadarProcessingHotPartitionState ClassifyQuarantined(
        int partitionId,
        int shardId,
        long evaluationSequence)
    {
        EnsurePartitionId(partitionId);
        var current = partitions[partitionId];
        var ineffectiveMoveCount = Math.Max(current.IneffectiveMoveCount, QuarantineIneffectiveMoveCount);
        return SetState(
            partitionId,
            shardId,
            RadarProcessingHotPartitionClassification.Quarantined,
            evaluationSequence,
            ineffectiveMoveCount);
    }

    /// <summary>
    /// Records expected versus actual relief after a move attempt.
    /// </summary>
    /// <returns>
    /// Movable state when relief was effective; otherwise movable or quarantined state
    /// depending on accumulated ineffective move count.
    /// </returns>
    public RadarProcessingHotPartitionState RecordMoveOutcome(
        int partitionId,
        int shardId,
        long evaluationSequence,
        double expectedRelief,
        double actualRelief)
    {
        EnsurePartitionId(partitionId);
        ThrowIfInvalidRelief(expectedRelief, nameof(expectedRelief));
        ThrowIfInvalidRelief(actualRelief, nameof(actualRelief));

        var minimumEffectiveRelief = expectedRelief * MinimumEffectiveReliefRatio;
        if (actualRelief >= minimumEffectiveRelief)
        {
            return SetState(
                partitionId,
                shardId,
                RadarProcessingHotPartitionClassification.MovableHot,
                evaluationSequence,
                ineffectiveMoveCount: 0);
        }

        var current = partitions[partitionId];
        var ineffectiveMoveCount = checked(current.IneffectiveMoveCount + 1);
        var classification = ineffectiveMoveCount >= QuarantineIneffectiveMoveCount
            ? RadarProcessingHotPartitionClassification.Quarantined
            : RadarProcessingHotPartitionClassification.MovableHot;

        return SetState(
            partitionId,
            shardId,
            classification,
            evaluationSequence,
            ineffectiveMoveCount);
    }

    /// <summary>
    /// Clears classification and ineffective move count for a partition.
    /// </summary>
    public RadarProcessingHotPartitionState Clear(
        int partitionId,
        long evaluationSequence)
    {
        EnsurePartitionId(partitionId);
        return SetState(
            partitionId,
            shardId: 0,
            RadarProcessingHotPartitionClassification.None,
            evaluationSequence,
            ineffectiveMoveCount: 0);
    }

    private RadarProcessingHotPartitionState SetState(
        int partitionId,
        int shardId,
        RadarProcessingHotPartitionClassification classification,
        long evaluationSequence,
        int ineffectiveMoveCount)
    {
        var state = CreateState(
            partitionId,
            shardId,
            classification,
            evaluationSequence,
            ineffectiveMoveCount);
        partitions[partitionId] = state;
        return state;
    }

    private static RadarProcessingHotPartitionState CreateState(
        int partitionId,
        int shardId,
        RadarProcessingHotPartitionClassification classification,
        long evaluationSequence,
        int ineffectiveMoveCount) =>
        new(
            partitionId,
            shardId,
            classification,
            evaluationSequence,
            ineffectiveMoveCount);

    private void EnsurePartitionId(int partitionId)
    {
        if ((uint)partitionId < (uint)PartitionCount)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(partitionId));
    }

    private static void ThrowIfInvalidRelief(
        double value,
        string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Relief must be finite and non-negative.");
        }
    }
}
