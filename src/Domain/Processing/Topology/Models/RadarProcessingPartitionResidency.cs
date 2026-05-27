namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingPartitionResidency
{
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

    public int PartitionId { get; }

    public long AgeEvaluations { get; }

    public int RequiredEvaluations { get; }

    public bool IsSatisfied => AgeEvaluations >= RequiredEvaluations;
}
