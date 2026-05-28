namespace RadarPulse.Infrastructure.Archive;

internal readonly record struct ArchiveTwoGateMomentProjectorState(
    int RadialSequenceNumber,
    int CurrentSweepSequenceNumber,
    int CurrentSweepElevationNumber,
    int CurrentSweepRadialCount);
