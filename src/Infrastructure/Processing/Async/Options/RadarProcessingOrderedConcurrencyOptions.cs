namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingOrderedConcurrencyOptions
{
    public const int DefaultActiveBatchCapacity = 4;

    public static RadarProcessingOrderedConcurrencyOptions Default { get; } =
        new(DefaultActiveBatchCapacity);

    public static RadarProcessingOrderedConcurrencyOptions Sequential { get; } =
        new(1);

    public RadarProcessingOrderedConcurrencyOptions(int activeBatchCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(activeBatchCapacity);

        ActiveBatchCapacity = activeBatchCapacity;
    }

    public int ActiveBatchCapacity { get; }

    public bool IsSequential => ActiveBatchCapacity == 1;
}
