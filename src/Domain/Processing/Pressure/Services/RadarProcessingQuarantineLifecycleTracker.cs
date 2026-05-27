namespace RadarPulse.Domain.Processing;

/// <summary>
/// Mutable tracker for quarantine lifecycle state across all partitions.
/// </summary>
/// <remarks>
/// The tracker stores one lifecycle state per partition and buffers emitted
/// transitions until callers drain them into rebalance telemetry.
/// </remarks>
public sealed class RadarProcessingQuarantineLifecycleTracker
{
    private readonly RadarProcessingQuarantineLifecycleState[] partitions;
    private readonly IReadOnlyList<RadarProcessingQuarantineLifecycleState> partitionView;
    private readonly List<RadarProcessingQuarantineTransition> pendingTransitions = new();
    private readonly RadarProcessingQuarantineLifecycleEvaluator evaluator;

    /// <summary>
    /// Creates a quarantine lifecycle tracker for a fixed partition count.
    /// </summary>
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

    /// <summary>
    /// Number of partitions tracked.
    /// </summary>
    public int PartitionCount { get; }

    /// <summary>
    /// Lifecycle options used by the tracker.
    /// </summary>
    public RadarProcessingQuarantineLifecycleOptions Options => evaluator.Options;

    /// <summary>
    /// Partition lifecycle states ordered by partition id.
    /// </summary>
    public IReadOnlyList<RadarProcessingQuarantineLifecycleState> Partitions => partitionView;

    /// <summary>
    /// Returns lifecycle state for a partition.
    /// </summary>
    public RadarProcessingQuarantineLifecycleState GetPartition(
        int partitionId)
    {
        EnsurePartitionId(partitionId);
        return partitions[partitionId];
    }

    /// <summary>
    /// Records pressure-window state as lifecycle evidence for a partition.
    /// </summary>
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

    /// <summary>
    /// Records explicit lifecycle evidence for a partition.
    /// </summary>
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
        if (result.Transition is not null)
        {
            pendingTransitions.Add(result.Transition);
        }

        partitions[partitionId] = result.State;
        return result;
    }

    /// <summary>
    /// Returns and clears pending lifecycle transitions.
    /// </summary>
    public IReadOnlyList<RadarProcessingQuarantineTransition> DrainTransitions()
    {
        if (pendingTransitions.Count == 0)
        {
            return Array.AsReadOnly(Array.Empty<RadarProcessingQuarantineTransition>());
        }

        var result = pendingTransitions.ToArray();

        pendingTransitions.Clear();
        return Array.AsReadOnly(result);
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
