namespace RadarPulse.Infrastructure.Processing;

public sealed record RadarProcessingSyntheticWorkloadOptions(
    int SourceCount = 16,
    int BatchCount = 4,
    int EventsPerBatch = 1024,
    int PayloadValuesPerEvent = 4)
{
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(SourceCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(BatchCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(EventsPerBatch);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(PayloadValuesPerEvent);

        if (PayloadValuesPerEvent > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PayloadValuesPerEvent),
                PayloadValuesPerEvent,
                "Payload values per event must fit into a stream event gate count.");
        }
    }
}
