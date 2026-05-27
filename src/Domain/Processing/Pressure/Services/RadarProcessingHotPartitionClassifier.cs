namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingHotPartitionClassifier
{
    private readonly RadarProcessingHotPartitionState[] partitions;

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

    public int PartitionCount { get; }

    public int QuarantineIneffectiveMoveCount { get; }

    public double MinimumEffectiveReliefRatio { get; }

    public RadarProcessingHotPartitionState GetPartition(int partitionId)
    {
        EnsurePartitionId(partitionId);
        return partitions[partitionId];
    }

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
