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
    static void PrintArchiveRadarEventBatchStreamCacheBenchmarkResult(
        ArchiveRadarEventBatchStreamCacheBenchmarkResult result)
    {
        Console.WriteLine($"Cache: {result.CachePath}");
        if (result.Date is { } date)
        {
            Console.WriteLine($"Date: {date:yyyy-MM-dd}");
        }

        if (result.RadarId is not null)
        {
            Console.WriteLine($"Radar: {result.RadarId}");
        }

        Console.WriteLine($"Decompressor: {result.Decompressor}");
        Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
        Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
        Console.WriteLine($"Parallelism: {FormatNumber(result.DegreeOfParallelism)}");
        Console.WriteLine("Stream format: normalized RadarEventBatch");
        Console.WriteLine($"Stream schema version: {result.StreamSchemaVersion}");
        Console.WriteLine($"Source-universe version: {result.SourceUniverseVersion}");
        Console.WriteLine($"Examined files per iteration: {FormatNumber(result.ExaminedFilesPerIteration)}");
        Console.WriteLine($"Skipped files per iteration: {FormatNumber(result.SkippedFilesPerIteration)}");
        Console.WriteLine($"Published files per iteration: {FormatNumber(result.PublishedFilesPerIteration)}");
        Console.WriteLine($"File size bytes per iteration: {FormatNumber(result.FileSizeBytesPerIteration)}");
        Console.WriteLine($"Compressed records per iteration: {FormatNumber(result.CompressedRecordsPerIteration)}");
        Console.WriteLine($"Compressed bytes per iteration: {FormatNumber(result.CompressedBytesPerIteration)}");
        Console.WriteLine($"Decompressed bytes per iteration: {FormatNumber(result.DecompressedBytesPerIteration)}");
        Console.WriteLine($"Batches per iteration: {FormatNumber(result.BatchesPerIteration)}");
        Console.WriteLine($"Stream events per iteration: {FormatNumber(result.EventsPerIteration)}");
        Console.WriteLine($"Payload bytes per iteration: {FormatNumber(result.PayloadBytesPerIteration)}");
        Console.WriteLine($"Payload values per iteration: {FormatNumber(result.PayloadValuesPerIteration)}");
        Console.WriteLine($"Raw value checksum per iteration: {FormatNumber(result.RawValueChecksumPerIteration)}");
        Console.WriteLine($"Total examined files: {FormatNumber(result.TotalExaminedFiles)}");
        Console.WriteLine($"Total skipped files: {FormatNumber(result.TotalSkippedFiles)}");
        Console.WriteLine($"Total published files: {FormatNumber(result.TotalPublishedFiles)}");
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
    }
}
