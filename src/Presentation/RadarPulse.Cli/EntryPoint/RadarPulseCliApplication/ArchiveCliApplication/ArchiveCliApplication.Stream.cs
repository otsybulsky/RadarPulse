using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using static CliFormat;

internal static partial class ArchiveCliApplication
{
    private static int StreamArchive(string[] args)
    {
        var options = ArchiveStreamOptions.Parse(args);
        var decompressor = ArchiveBZip2Decompressors.Create(options.Decompressor);
        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        var result = new NexradArchiveRadarEventBatchPublisher(decompressor)
            .PublishFile(
                options.FilePath,
                new ArchiveRadarEventBatchPublishOptions(sourceUniverse, options.Parallelism),
                CancellationToken.None);

        PrintArchiveRadarEventBatchPublishResult(result, sourceUniverse);
        return 0;
    }

    private static void PrintArchiveRadarEventBatchPublishResult(
        ArchiveRadarEventBatchPublishResult result,
        RadarSourceUniverse sourceUniverse)
    {
        var dictionaryMetrics = RadarStreamDictionarySnapshotMetrics.Compute(result.DictionarySnapshot);

        Console.WriteLine($"File: {result.FilePath}");
        Console.WriteLine($"Decompressor: {result.Decompressor}");
        Console.WriteLine($"Parallelism: {FormatNumber(result.DegreeOfParallelism)}");
        Console.WriteLine("Stream format: normalized RadarEventBatch");
        Console.WriteLine($"Stream schema version: {result.StreamSchemaVersion}");
        Console.WriteLine($"Dictionary version: {result.DictionaryVersion}");
        Console.WriteLine($"Source-universe version: {result.SourceUniverseVersion}");
        Console.WriteLine($"Logical sources: {FormatNumber(sourceUniverse.SourceCount)}");
        Console.WriteLine($"File size bytes: {FormatNumber(result.FileSizeBytes)}");
        Console.WriteLine($"Compressed records: {FormatNumber(result.CompressedRecordCount)}");
        Console.WriteLine($"Compressed bytes: {FormatNumber(result.CompressedBytes)}");
        Console.WriteLine($"Decompressed bytes: {FormatNumber(result.DecompressedBytes)}");
        Console.WriteLine($"Batches: {FormatNumber(result.BatchCount)}");
        Console.WriteLine($"Events: {FormatNumber(result.EventCount)}");
        Console.WriteLine($"Payload bytes: {FormatNumber(result.PayloadBytes)}");
        Console.WriteLine($"Payload values: {FormatNumber(result.PayloadValueCount)}");
        Console.WriteLine($"Raw value checksum: {FormatNumber(result.RawValueChecksum)}");
        Console.WriteLine($"Radar dictionary entries: {FormatNumber(dictionaryMetrics.RadarCount)}");
        Console.WriteLine($"Moment dictionary entries: {FormatNumber(dictionaryMetrics.MomentCount)}");
        Console.WriteLine($"Dictionary mapping checksum: {FormatUnsignedNumber(dictionaryMetrics.MappingChecksum)}");
    }
}
