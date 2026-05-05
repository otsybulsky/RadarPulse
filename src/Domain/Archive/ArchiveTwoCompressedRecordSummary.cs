namespace RadarPulse.Domain.Archive;

public sealed record ArchiveTwoCompressedRecordSummary(
    int SequenceNumber,
    long ControlWordOffset,
    int ControlWord,
    int CompressedSizeBytes,
    bool StartsWithBZip2Signature,
    long? DecompressedSizeBytes,
    string? DecompressionDiagnostic);
