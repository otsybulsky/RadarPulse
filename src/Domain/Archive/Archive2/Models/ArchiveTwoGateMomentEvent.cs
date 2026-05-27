namespace RadarPulse.Domain.Archive;

/// <summary>
/// Projected gate-level moment value from an Archive II type 31 radial message.
/// </summary>
public readonly record struct ArchiveTwoGateMomentEvent(
    string RadarId,
    DateTimeOffset VolumeTimestamp,
    DateTimeOffset MessageTimestamp,
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

/// <summary>
/// Archive II generic moment data-code interpretation for one gate value.
/// </summary>
public enum ArchiveTwoGateMomentStatus
{
    /// <summary>
    /// Gate contains a calibrated meteorological value.
    /// </summary>
    Valid = 0,

    /// <summary>
    /// Gate value is below the product threshold.
    /// </summary>
    BelowThreshold = 1,

    /// <summary>
    /// Gate value is range folded.
    /// </summary>
    RangeFolded = 2,

    /// <summary>
    /// Clutter filtering was not applied to the gate value.
    /// </summary>
    ClutterFilterNotApplied = 3,

    /// <summary>
    /// Point clutter filtering was applied to the gate value.
    /// </summary>
    PointClutterFilterApplied = 4,

    /// <summary>
    /// Dual-polarization filtering was applied to the gate value.
    /// </summary>
    DualPolarizationFiltered = 5,

    /// <summary>
    /// Gate value is reserved by the source data contract.
    /// </summary>
    Reserved = 6,

    /// <summary>
    /// Gate value uses an unsupported or unknown data-code interpretation.
    /// </summary>
    Unsupported = 7
}
