namespace RadarPulse.Domain.Archive;

public sealed record ArchiveTwoMessageSummary(
    int MessageCount,
    IReadOnlyList<ArchiveTwoMessageTypeCount> MessageTypes,
    ArchiveTwoType31Summary Type31);

public sealed record ArchiveTwoMessageTypeCount(
    int MessageType,
    int Count);

public sealed record ArchiveTwoType31Summary(
    int RadialCount,
    long EstimatedGateMomentEventCount,
    ArchiveTwoType31ConstantBlockSummary ConstantBlocks,
    IReadOnlyList<ArchiveTwoMomentSummary> Moments,
    IReadOnlyList<ArchiveTwoSweepSummary> Sweeps);

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

public sealed record ArchiveTwoType31ConstantBlockSummary(
    int VolumeCount,
    int ElevationCount,
    int RadialCount);

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

public readonly record struct ArchiveTwoMessageSource(
    int CompressedRecordSequenceNumber,
    int MessageSequenceNumberInRecord);

public readonly record struct ArchiveTwoRadialSourceOrder(
    int CompressedRecordSequenceNumber,
    int MessageSequenceNumberInRecord,
    int Type31RadialSequenceNumber);
