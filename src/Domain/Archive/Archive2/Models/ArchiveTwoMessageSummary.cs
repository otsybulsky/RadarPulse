namespace RadarPulse.Domain.Archive;

/// <summary>
/// Aggregate summary of RDA/RPG messages and type 31 radial content in Archive II data.
/// </summary>
public sealed record ArchiveTwoMessageSummary(
    int MessageCount,
    IReadOnlyList<ArchiveTwoMessageTypeCount> MessageTypes,
    ArchiveTwoType31Summary Type31);

/// <summary>
/// Count of messages observed for one RDA/RPG message type.
/// </summary>
public sealed record ArchiveTwoMessageTypeCount(
    int MessageType,
    int Count);

/// <summary>
/// Summary of Archive II type 31 radial and moment content.
/// </summary>
public sealed record ArchiveTwoType31Summary(
    int RadialCount,
    long EstimatedGateMomentEventCount,
    ArchiveTwoType31ConstantBlockSummary ConstantBlocks,
    IReadOnlyList<ArchiveTwoMomentSummary> Moments,
    IReadOnlyList<ArchiveTwoSweepSummary> Sweeps);

/// <summary>
/// Summary of one type 31 generic moment block across observed radials.
/// </summary>
public sealed record ArchiveTwoMomentSummary(
    string Name,
    int RadialCount,
    long GateCount,
    int MinimumGateCount,
    int MaximumGateCount,
    int MinimumWordSizeBits,
    int MaximumWordSizeBits,
    float MinimumFirstGateRangeKilometers,
    float MaximumFirstGateRangeKilometers,
    float MinimumGateSpacingKilometers,
    float MaximumGateSpacingKilometers,
    float MinimumScale,
    float MaximumScale,
    float MinimumOffset,
    float MaximumOffset);

/// <summary>
/// Counts of type 31 volume, elevation, and radial constant blocks.
/// </summary>
public sealed record ArchiveTwoType31ConstantBlockSummary(
    int VolumeCount,
    int ElevationCount,
    int RadialCount);

/// <summary>
/// Summary of one sweep assembled from type 31 radial metadata.
/// </summary>
public sealed record ArchiveTwoSweepSummary(
    int SequenceNumber,
    int ElevationNumber,
    int MinimumCutSectorNumber,
    int MaximumCutSectorNumber,
    int RadialCount,
    int StartRadialStatus,
    int EndRadialStatus,
    float MinimumElevationAngleDegrees,
    float MaximumElevationAngleDegrees,
    float AverageElevationAngleDegrees,
    int VolumeConstantBlockCount,
    int ElevationConstantBlockCount,
    int RadialConstantBlockCount,
    IReadOnlyList<string> Moments,
    ArchiveTwoRadialSourceOrder FirstRadial,
    ArchiveTwoRadialSourceOrder LastRadial);

/// <summary>
/// Source position and timestamp metadata for one decompressed RDA/RPG message.
/// </summary>
public readonly record struct ArchiveTwoMessageSource(
    int CompressedRecordSequenceNumber,
    int MessageSequenceNumberInRecord,
    DateOnly MessageDate,
    TimeSpan MessageTime)
{
    /// <summary>
    /// Gets the UTC message timestamp derived from the Archive II message date and time fields.
    /// </summary>
    public DateTimeOffset MessageTimestamp =>
        new DateTimeOffset(MessageDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).Add(MessageTime);
}

/// <summary>
/// Stable source-order key for replay chronology across compressed record, message, and radial sequence.
/// </summary>
public readonly record struct ArchiveTwoRadialSourceOrder(
    int CompressedRecordSequenceNumber,
    int MessageSequenceNumberInRecord,
    int Type31RadialSequenceNumber);
