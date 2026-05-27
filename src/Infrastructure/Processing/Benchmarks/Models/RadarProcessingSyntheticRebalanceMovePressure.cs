using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Pressure evidence recorded for an accepted synthetic rebalance move.
/// </summary>
public readonly record struct RadarProcessingSyntheticRebalanceMovePressure(
    /// <summary>
    /// Accepted rebalance move kind.
    /// </summary>
    RadarProcessingRebalanceMoveKind MoveKind,
    /// <summary>
    /// Source shard pressure before the move.
    /// </summary>
    double SourceShardBefore,
    /// <summary>
    /// Target shard pressure before the move.
    /// </summary>
    double TargetShardBefore,
    /// <summary>
    /// Source shard pressure after the move.
    /// </summary>
    double SourceShardAfter,
    /// <summary>
    /// Target shard pressure after the move.
    /// </summary>
    double TargetShardAfter,
    /// <summary>
    /// Expected pressure relief from the move.
    /// </summary>
    double ExpectedRelief);
