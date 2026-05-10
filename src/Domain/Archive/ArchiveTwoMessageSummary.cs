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
    IReadOnlyList<ArchiveTwoMomentSummary> Moments);

public sealed record ArchiveTwoMomentSummary(
    string Name,
    int RadialCount,
    long GateCount);
