namespace RadarPulse.Domain.Processing;

/// <summary>
/// Merge contract for handlers that can combine per-batch deltas deterministically.
/// </summary>
public interface IRadarProcessingHandlerDeltaMerger
{
    /// <summary>
    /// Handler name expected on incoming deltas.
    /// </summary>
    string HandlerName { get; }

    /// <summary>
    /// Contract version expected on incoming deltas.
    /// </summary>
    string HandlerContractVersion { get; }

    /// <summary>
    /// Merges current exported values with one incoming delta.
    /// </summary>
    IReadOnlyList<RadarProcessingHandlerDeltaValue> Merge(
        IReadOnlyList<RadarProcessingHandlerDeltaValue> currentValues,
        RadarProcessingHandlerDelta delta);
}
