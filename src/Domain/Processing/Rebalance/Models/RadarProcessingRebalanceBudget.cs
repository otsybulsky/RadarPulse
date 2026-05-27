namespace RadarPulse.Domain.Processing;

public readonly record struct RadarProcessingRebalanceBudget
{
    public RadarProcessingRebalanceBudget(
        int limit,
        int used)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        ArgumentOutOfRangeException.ThrowIfNegative(used);

        Limit = limit;
        Used = used;
    }

    public int Limit { get; }

    public int Used { get; }

    public int Remaining => Math.Max(0, Limit - Used);

    public bool IsExhausted => Used >= Limit;
}
