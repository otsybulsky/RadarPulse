namespace RadarPulse.Domain.Processing;

/// <summary>
/// Pressure classification used by routing, pressure windows, and rebalance planners.
/// </summary>
public enum RadarProcessingPressureBand
{
    /// <summary>
    /// No meaningful pressure was observed.
    /// </summary>
    Cold = 0,

    /// <summary>
    /// Pressure is present but below warm thresholds.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Pressure is elevated but not yet a hot rebalance source.
    /// </summary>
    Warm = 2,

    /// <summary>
    /// Pressure is high enough to be considered for rebalance relief.
    /// </summary>
    Hot = 3,

    /// <summary>
    /// Pressure exceeds the highest configured band.
    /// </summary>
    SuperHot = 4
}
