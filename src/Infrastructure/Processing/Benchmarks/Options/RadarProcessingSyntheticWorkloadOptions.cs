namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Shape of deterministic synthetic processing workload batches.
/// </summary>
public sealed record RadarProcessingSyntheticWorkloadOptions(
    /// <summary>
    /// Number of dense sources to distribute events across.
    /// </summary>
    int SourceCount = 16,
    /// <summary>
    /// Number of batches per benchmark iteration.
    /// </summary>
    int BatchCount = 4,
    /// <summary>
    /// Number of events per batch.
    /// </summary>
    int EventsPerBatch = 1024,
    /// <summary>
    /// Number of payload values generated for each event.
    /// </summary>
    int PayloadValuesPerEvent = 4)
{
    /// <summary>
    /// Validates positive workload dimensions and stream event gate-count limits.
    /// </summary>
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
