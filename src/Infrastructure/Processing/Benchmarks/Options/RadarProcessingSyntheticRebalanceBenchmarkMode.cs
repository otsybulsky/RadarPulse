namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Processing mode measured by the synthetic rebalance benchmark.
/// </summary>
public enum RadarProcessingSyntheticRebalanceBenchmarkMode
{
    /// <summary>
    /// Process batches without rebalance decisions.
    /// </summary>
    StaticNoRebalance = 0,

    /// <summary>
    /// Record pressure samples without applying rebalance sessions.
    /// </summary>
    PressureSamplingOnly,

    /// <summary>
    /// Run the standard rebalance session.
    /// </summary>
    RebalanceSession,

    /// <summary>
    /// Run ordered concurrent rebalance session processing.
    /// </summary>
    OrderedRebalanceSession
}
