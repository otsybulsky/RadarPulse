namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingQuarantineLifecycleTracker
{
    private readonly RadarProcessingQuarantineLifecycleState[] partitions;
    private readonly IReadOnlyList<RadarProcessingQuarantineLifecycleState> partitionView;
    private readonly RadarProcessingQuarantineLifecycleEvaluator evaluator;

    public RadarProcessingQuarantineLifecycleTracker(
        int partitionCount,
        RadarProcessingQuarantineLifecycleOptions? options = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);

        PartitionCount = partitionCount;
        evaluator = new RadarProcessingQuarantineLifecycleEvaluator(options);
        partitions = new RadarProcessingQuarantineLifecycleState[partitionCount];

        for (var partitionId = 0; partitionId < partitions.Length; partitionId++)
        {
            partitions[partitionId] = RadarProcessingQuarantineLifecycleState.Unclassified(partitionId);
        }

        partitionView = Array.AsReadOnly(partitions);
    }

    public int PartitionCount { get; }

    public RadarProcessingQuarantineLifecycleOptions Options => evaluator.Options;

    public IReadOnlyList<RadarProcessingQuarantineLifecycleState> Partitions => partitionView;

    public RadarProcessingQuarantineLifecycleState GetPartition(
        int partitionId)
    {
        EnsurePartitionId(partitionId);
        return partitions[partitionId];
    }

    public RadarProcessingQuarantineLifecycleEvaluationResult RecordPartitionEvidence(
        RadarProcessingPartitionPressureState partition,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingHotPartitionClassification observedClassification) =>
        RecordEvidence(
            partition.PartitionId,
            partition.ShardId,
            evaluationSequence,
            topologyVersion,
            partition.AverageScore,
            partition.Band,
            observedClassification);

    public RadarProcessingQuarantineLifecycleEvaluationResult RecordEvidence(
        int partitionId,
        int shardId,
        long evaluationSequence,
        RadarProcessingTopologyVersion topologyVersion,
        RadarProcessingPressureScore pressure,
        RadarProcessingPressureBand band,
        RadarProcessingHotPartitionClassification observedClassification)
    {
        EnsurePartitionId(partitionId);

        var evidence = new RadarProcessingQuarantineEvidence(
            partitionId,
            shardId,
            evaluationSequence,
            topologyVersion,
            pressure,
            band,
            observedClassification);
        var result = evaluator.Evaluate(partitions[partitionId], evidence);

        partitions[partitionId] = result.State;
        return result;
    }

    private void EnsurePartitionId(
        int partitionId)
    {
        if ((uint)partitionId < (uint)PartitionCount)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(partitionId));
    }
}
