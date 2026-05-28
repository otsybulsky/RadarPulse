using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;
using static CliFormat;

internal static partial class ArchiveBenchmarkCliApplication
{
    static int BenchmarkArchiveStream(string[] args)
    {
        var options = ArchiveBenchmarkStreamOptions.Parse(args);
        if (options.CachePath is not null)
        {
            var cacheResult = new NexradArchiveRadarEventBatchStreamBenchmark().MeasureCache(
                options.CachePath,
                options.Date,
                options.RadarId,
                options.MaxFiles,
                options.Iterations,
                options.WarmupIterations,
                options.Parallelism,
                options.Decompressor,
                CancellationToken.None);
            PrintArchiveRadarEventBatchStreamCacheBenchmarkResult(cacheResult);
            return 0;
        }

        var result = new NexradArchiveRadarEventBatchStreamBenchmark().Measure(
            options.FilePath ?? throw new InvalidOperationException("--file is required when --cache is not provided."),
            options.Iterations,
            options.WarmupIterations,
            options.Parallelism,
            options.Decompressor,
            CancellationToken.None);

        Console.WriteLine($"File: {result.FilePath}");
        Console.WriteLine($"Decompressor: {result.Decompressor}");
        Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
        Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
        Console.WriteLine($"Parallelism: {FormatNumber(result.DegreeOfParallelism)}");
        Console.WriteLine("Stream format: normalized RadarEventBatch");
        Console.WriteLine($"Stream schema version: {result.StreamSchemaVersion}");
        Console.WriteLine($"Dictionary version: {result.DictionaryVersion}");
        Console.WriteLine($"Source-universe version: {result.SourceUniverseVersion}");
        Console.WriteLine($"Radar dictionary entries: {FormatNumber(result.RadarDictionaryEntries)}");
        Console.WriteLine($"Moment dictionary entries: {FormatNumber(result.MomentDictionaryEntries)}");
        Console.WriteLine($"Dictionary mapping checksum: {FormatUnsignedNumber(result.DictionaryMappingChecksum)}");
        Console.WriteLine($"File size bytes: {FormatNumber(result.FileSizeBytes)}");
        Console.WriteLine($"Compressed records per iteration: {FormatNumber(result.CompressedRecordsPerIteration)}");
        Console.WriteLine($"Compressed bytes per iteration: {FormatNumber(result.CompressedBytesPerIteration)}");
        Console.WriteLine($"Decompressed bytes per iteration: {FormatNumber(result.DecompressedBytesPerIteration)}");
        Console.WriteLine($"Batches per iteration: {FormatNumber(result.BatchesPerIteration)}");
        Console.WriteLine($"Stream events per iteration: {FormatNumber(result.EventsPerIteration)}");
        Console.WriteLine($"Payload bytes per iteration: {FormatNumber(result.PayloadBytesPerIteration)}");
        Console.WriteLine($"Payload values per iteration: {FormatNumber(result.PayloadValuesPerIteration)}");
        Console.WriteLine($"Raw value checksum per iteration: {FormatNumber(result.RawValueChecksumPerIteration)}");
        Console.WriteLine($"Total compressed records: {FormatNumber(result.TotalCompressedRecords)}");
        Console.WriteLine($"Total compressed bytes: {FormatNumber(result.TotalCompressedBytes)}");
        Console.WriteLine($"Total decompressed bytes: {FormatNumber(result.TotalDecompressedBytes)}");
        Console.WriteLine($"Total batches: {FormatNumber(result.TotalBatches)}");
        Console.WriteLine($"Total stream events: {FormatNumber(result.TotalEvents)}");
        Console.WriteLine($"Total payload bytes: {FormatNumber(result.TotalPayloadBytes)}");
        Console.WriteLine($"Total payload values: {FormatNumber(result.TotalPayloadValues)}");
        Console.WriteLine($"Elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
        Console.WriteLine($"Compressed MB/s: {FormatDecimal(MegabytesPerSecond(result.TotalCompressedBytes, result.Elapsed))}");
        Console.WriteLine($"Decompressed MB/s: {FormatDecimal(MegabytesPerSecond(result.TotalDecompressedBytes, result.Elapsed))}");
        Console.WriteLine($"Batches/s: {FormatDecimal(PerSecond(result.TotalBatches, result.Elapsed))}");
        Console.WriteLine($"Stream events/s: {FormatDecimal(PerSecond(result.TotalEvents, result.Elapsed))}");
        Console.WriteLine($"Payload values/s: {FormatDecimal(PerSecond(result.TotalPayloadValues, result.Elapsed))}");
        Console.WriteLine($"Allocated bytes: {FormatNumber(result.AllocatedBytes)}");
        Console.WriteLine($"Allocated bytes / stream event: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalEvents, 1d))}");
        Console.WriteLine($"Allocated bytes / payload value: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalPayloadValues, 1d))}");
        return 0;
    }
}
