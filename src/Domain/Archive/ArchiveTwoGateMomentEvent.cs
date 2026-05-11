namespace RadarPulse.Domain.Archive;

public readonly record struct ArchiveTwoGateMomentEvent(
    string RadarId,
    DateTimeOffset VolumeTimestamp,
    int SweepSequenceNumber,
    int ElevationNumber,
    int RadialSequenceNumber,
    int GateIndex,
    float RangeKilometers,
    string MomentName,
    int RawValue,
    ArchiveTwoGateMomentStatus Status,
    double? CalibratedValue,
    ArchiveTwoRadialSourceOrder SourceOrder);

public enum ArchiveTwoGateMomentStatus
{
    Valid = 0,
    BelowThreshold = 1,
    RangeFolded = 2,
    ClutterFilterNotApplied = 3,
    PointClutterFilterApplied = 4,
    DualPolarizationFiltered = 5,
    Reserved = 6,
    Unsupported = 7
}
