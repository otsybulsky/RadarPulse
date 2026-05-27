using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public readonly record struct RadarProcessingSyntheticRebalanceMovePressure(
    RadarProcessingRebalanceMoveKind MoveKind,
    double SourceShardBefore,
    double TargetShardBefore,
    double SourceShardAfter,
    double TargetShardAfter,
    double ExpectedRelief);
