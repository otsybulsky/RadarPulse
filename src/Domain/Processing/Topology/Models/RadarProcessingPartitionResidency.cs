namespace RadarPulse.Domain.Processing;

/// <summary>
/// Tracks how long a partition has stayed on its current owner.
/// </summary>
/// <remarks>
/// Rebalance policy uses residency to prevent a partition from moving again
/// before the configured number of evaluations has elapsed.
/// </remarks>
public readonly record struct RadarProcessingPartitionResidency
{
    /// <summary>
    /// Creates a residency measurement for a partition.
    /// </summary>
    public RadarProcessingPartitionResidency(
        int partitionId,
        long ageEvaluations,
        int requiredEvaluations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(ageEvaluations);
        ArgumentOutOfRangeException.ThrowIfNegative(requiredEvaluations);

        PartitionId = partitionId;
        AgeEvaluations = ageEvaluations;
        RequiredEvaluations = requiredEvaluations;
    }

    /// <summary>
    /// Partition whose owner residency is being measured.
    /// </summary>
    public int PartitionId { get; }

    /// <summary>
    /// Number of rebalance evaluations since the partition last moved.
    /// </summary>
    public long AgeEvaluations { get; }

    /// <summary>
    /// Required residency before another move is policy-eligible.
    /// </summary>
    public int RequiredEvaluations { get; }

    /// <summary>
    /// Indicates whether the partition has satisfied the configured residency.
    /// </summary>
    public bool IsSatisfied => AgeEvaluations >= RequiredEvaluations;
}
