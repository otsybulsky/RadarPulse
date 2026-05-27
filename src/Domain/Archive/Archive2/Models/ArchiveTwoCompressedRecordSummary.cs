namespace RadarPulse.Domain.Archive;

/// <summary>
/// Summary of one compressed Archive II record and its decompression probe result.
/// </summary>
public sealed record ArchiveTwoCompressedRecordSummary(
    int SequenceNumber,
    long ControlWordOffset,
    int ControlWord,
    int CompressedSizeBytes,
    bool StartsWithBZip2Signature,
    long? DecompressedSizeBytes,
    string? DecompressionDiagnostic);
