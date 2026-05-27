namespace RadarPulse.Domain.Processing;

/// <summary>
/// Represents cumulative processing totals and the deterministic checksum of active source state.
/// </summary>
/// <param name="ProcessedBatchCount">Number of successfully committed batches.</param>
/// <param name="ProcessedStreamEventCount">Number of stream events applied across active sources.</param>
/// <param name="ProcessedPayloadValueCount">Number of payload values applied across active sources.</param>
/// <param name="ActiveSourceCount">Number of sources with at least one processed event.</param>
/// <param name="RawValueChecksum">Additive checksum of raw payload values across active sources.</param>
/// <param name="ProcessingChecksum">Deterministic checksum over active source processing state.</param>
public readonly record struct RadarProcessingMetrics(
    long ProcessedBatchCount,
    long ProcessedStreamEventCount,
    long ProcessedPayloadValueCount,
    long ActiveSourceCount,
    long RawValueChecksum,
    ulong ProcessingChecksum)
{
    /// <summary>
    /// Gets an all-zero metrics value.
    /// </summary>
    public static RadarProcessingMetrics Empty => default;
}
