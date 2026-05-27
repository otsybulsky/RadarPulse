using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Archive;

/// <summary>
/// Result for publishing stream-oriented radar event batches from one Archive II file.
/// </summary>
public sealed record ArchiveRadarEventBatchPublishResult(
    string FilePath,
    string Decompressor,
    int DegreeOfParallelism,
    long FileSizeBytes,
    int CompressedRecordCount,
    long CompressedBytes,
    long DecompressedBytes,
    StreamSchemaVersion StreamSchemaVersion,
    DictionaryVersion DictionaryVersion,
    SourceUniverseVersion SourceUniverseVersion,
    long BatchCount,
    long EventCount,
    long PayloadBytes,
    long PayloadValueCount,
    long RawValueChecksum,
    RadarStreamDictionarySnapshot DictionarySnapshot);
