namespace RadarPulse.Domain.Processing;

/// <summary>
/// Factory for per-coordinator handler delta accumulators.
/// </summary>
public interface IRadarProcessingHandlerDeltaAccumulatorFactory
{
    /// <summary>
    /// Creates a fresh accumulator for one ordered merge coordinator.
    /// </summary>
    IRadarProcessingHandlerDeltaAccumulator CreateAccumulator();
}
