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
    static void PrintArchiveReplayPublishCacheBenchmarkResult(ArchiveReplayPublishCacheBenchmarkResult result)
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
        Console.WriteLine("Chronology verification: required");
        Console.WriteLine($"Examined files per iteration: {FormatNumber(result.ExaminedFilesPerIteration)}");
        Console.WriteLine($"Skipped files per iteration: {FormatNumber(result.SkippedFilesPerIteration)}");
        Console.WriteLine($"Published files per iteration: {FormatNumber(result.PublishedFilesPerIteration)}");
        Console.WriteLine($"File size bytes per iteration: {FormatNumber(result.FileSizeBytesPerIteration)}");
        Console.WriteLine($"Compressed records per iteration: {FormatNumber(result.CompressedRecordsPerIteration)}");
        Console.WriteLine($"Compressed bytes per iteration: {FormatNumber(result.CompressedBytesPerIteration)}");
        Console.WriteLine($"Decompressed bytes per iteration: {FormatNumber(result.DecompressedBytesPerIteration)}");
        Console.WriteLine($"Published events per iteration: {FormatNumber(result.PublishedEventsPerIteration)}");
        Console.WriteLine($"Valid events per iteration: {FormatNumber(result.ValidEventsPerIteration)}");
        Console.WriteLine($"Below-threshold events per iteration: {FormatNumber(result.BelowThresholdEventsPerIteration)}");
        Console.WriteLine($"Range-folded events per iteration: {FormatNumber(result.RangeFoldedEventsPerIteration)}");
        Console.WriteLine($"CFP filter-not-applied events per iteration: {FormatNumber(result.ClutterFilterNotAppliedEventsPerIteration)}");
        Console.WriteLine($"CFP point-clutter-filter events per iteration: {FormatNumber(result.PointClutterFilterAppliedEventsPerIteration)}");
        Console.WriteLine($"CFP dual-pol-filtered events per iteration: {FormatNumber(result.DualPolarizationFilteredEventsPerIteration)}");
        Console.WriteLine($"Reserved events per iteration: {FormatNumber(result.ReservedEventsPerIteration)}");
        Console.WriteLine($"Unsupported events per iteration: {FormatNumber(result.UnsupportedEventsPerIteration)}");
        Console.WriteLine($"Raw value checksum per iteration: {FormatNumber(result.RawValueChecksumPerIteration)}");
        Console.WriteLine($"Calibrated value scaled checksum per iteration: {FormatNumber(result.CalibratedValueScaledChecksumPerIteration)}");
        Console.WriteLine($"Chronology checksum per iteration: {FormatUnsignedNumber(result.ChronologyChecksumPerIteration)}");
        Console.WriteLine($"Total examined files: {FormatNumber(result.TotalExaminedFiles)}");
        Console.WriteLine($"Total skipped files: {FormatNumber(result.TotalSkippedFiles)}");
        Console.WriteLine($"Total published files: {FormatNumber(result.TotalPublishedFiles)}");
        Console.WriteLine($"Total compressed records: {FormatNumber(result.TotalCompressedRecords)}");
        Console.WriteLine($"Total compressed bytes: {FormatNumber(result.TotalCompressedBytes)}");
        Console.WriteLine($"Total decompressed bytes: {FormatNumber(result.TotalDecompressedBytes)}");
        Console.WriteLine($"Total published events: {FormatNumber(result.TotalPublishedEvents)}");
        Console.WriteLine($"Total valid events: {FormatNumber(result.TotalValidEvents)}");
        Console.WriteLine($"Total below-threshold events: {FormatNumber(result.TotalBelowThresholdEvents)}");
        Console.WriteLine($"Total range-folded events: {FormatNumber(result.TotalRangeFoldedEvents)}");
        Console.WriteLine($"Total CFP filter-not-applied events: {FormatNumber(result.TotalClutterFilterNotAppliedEvents)}");
        Console.WriteLine($"Total CFP point-clutter-filter events: {FormatNumber(result.TotalPointClutterFilterAppliedEvents)}");
        Console.WriteLine($"Total CFP dual-pol-filtered events: {FormatNumber(result.TotalDualPolarizationFilteredEvents)}");
        Console.WriteLine($"Total reserved events: {FormatNumber(result.TotalReservedEvents)}");
        Console.WriteLine($"Total unsupported events: {FormatNumber(result.TotalUnsupportedEvents)}");
        Console.WriteLine($"Elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
        Console.WriteLine($"Compressed MB/s: {FormatDecimal(MegabytesPerSecond(result.TotalCompressedBytes, result.Elapsed))}");
        Console.WriteLine($"Decompressed MB/s: {FormatDecimal(MegabytesPerSecond(result.TotalDecompressedBytes, result.Elapsed))}");
        Console.WriteLine($"Published events/s: {FormatDecimal(PerSecond(result.TotalPublishedEvents, result.Elapsed))}");
        Console.WriteLine($"Valid events/s: {FormatDecimal(PerSecond(result.TotalValidEvents, result.Elapsed))}");
        Console.WriteLine($"Allocated bytes: {FormatNumber(result.AllocatedBytes)}");
        Console.WriteLine($"Allocated bytes / event: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalPublishedEvents, 1d))}");
        Console.WriteLine($"Allocated bytes / valid event: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalValidEvents, 1d))}");
        if (result.PublishedFilesPerIteration == 0)
        {
            Console.WriteLine("Diagnostic: no Archive Two base-data files were selected for replay-publish benchmarking.");
        }
    }

}
