namespace RadarPulse.Domain.Processing;

/// <summary>
/// Stateful accumulator used by mergeable handlers to combine ordered deltas.
/// </summary>
public interface IRadarProcessingHandlerDeltaAccumulator
{
    /// <summary>
    /// Merges one validated handler delta into accumulated state.
    /// </summary>
    IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
        RadarProcessingHandlerDelta delta);

    /// <summary>
    /// Creates an immutable snapshot of currently merged handler values.
    /// </summary>
    IReadOnlyList<RadarProcessingHandlerDeltaValue> CreateMergedValuesSnapshot();
}
