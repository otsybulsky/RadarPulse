using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;
using static CliFormat;

internal static partial class ArchiveCliApplication
{
    private static int ReplayArchive(string[] args)
    {
        var options = ArchiveReplayOptions.Parse(args);
        var decompressor = ArchiveBZip2Decompressors.Create(options.Decompressor);
        if (options.FilePath is not null)
        {
            var result = new NexradArchiveReplayPublisher(decompressor)
                .PublishFile(
                    options.FilePath,
                    new ArchiveReplayPublishOptions(options.Parallelism),
                    CancellationToken.None);
            PrintArchiveReplayPublishResult(result);
            return 0;
        }

        using var session = new NexradArchiveReplayPublishSession(decompressor, options.Parallelism);
        var cacheResult = session.PublishCache(
            options.CachePath ?? throw new InvalidOperationException("--cache is required when --file is not provided."),
            options.Date,
            options.RadarId,
            options.MaxFiles,
            CancellationToken.None);
        PrintArchiveReplayCachePublishResult(cacheResult);
        return 0;
    }

    private static void PrintArchiveReplayPublishResult(ArchiveReplayPublishResult result)
    {
        Console.WriteLine($"File: {result.FilePath}");
        Console.WriteLine($"Decompressor: {result.Decompressor}");
        Console.WriteLine($"Parallelism: {FormatNumber(result.DegreeOfParallelism)}");
        Console.WriteLine("Chronology verification: required");
        Console.WriteLine($"File size bytes: {FormatNumber(result.FileSizeBytes)}");
        Console.WriteLine($"Compressed records: {FormatNumber(result.CompressedRecordCount)}");
        Console.WriteLine($"Compressed bytes: {FormatNumber(result.CompressedBytes)}");
        Console.WriteLine($"Decompressed bytes: {FormatNumber(result.DecompressedBytes)}");
        Console.WriteLine($"Published events: {FormatNumber(result.PublishedEvents)}");
        Console.WriteLine($"Valid events: {FormatNumber(result.ValidEvents)}");
        Console.WriteLine($"Valid event share: {FormatPercent(result.ValidEventShare)}");
        Console.WriteLine($"Below-threshold events: {FormatNumber(result.BelowThresholdEvents)}");
        Console.WriteLine($"Range-folded events: {FormatNumber(result.RangeFoldedEvents)}");
        Console.WriteLine($"CFP filter-not-applied events: {FormatNumber(result.ClutterFilterNotAppliedEvents)}");
        Console.WriteLine($"CFP point-clutter-filter events: {FormatNumber(result.PointClutterFilterAppliedEvents)}");
        Console.WriteLine($"CFP dual-pol-filtered events: {FormatNumber(result.DualPolarizationFilteredEvents)}");
        Console.WriteLine($"Reserved events: {FormatNumber(result.ReservedEvents)}");
        Console.WriteLine($"Unsupported events: {FormatNumber(result.UnsupportedEvents)}");
        Console.WriteLine($"Raw value checksum: {FormatNumber(result.RawValueChecksum)}");
        Console.WriteLine($"Calibrated value scaled checksum: {FormatNumber(result.CalibratedValueScaledChecksum)}");
        Console.WriteLine($"Chronology checksum: {FormatUnsignedNumber(result.ChronologyChecksum)}");
    }

    private static void PrintArchiveReplayCachePublishResult(ArchiveReplayCachePublishResult result)
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
        Console.WriteLine($"Parallelism: {FormatNumber(result.DegreeOfParallelism)}");
        Console.WriteLine("Chronology verification: required");
        Console.WriteLine($"Examined files: {FormatNumber(result.ExaminedFileCount)}");
        Console.WriteLine($"Skipped files: {FormatNumber(result.SkippedFileCount)}");
        Console.WriteLine($"Published files: {FormatNumber(result.PublishedFileCount)}");
        Console.WriteLine($"File size bytes: {FormatNumber(result.TotalFileSizeBytes)}");
        Console.WriteLine($"Compressed records: {FormatNumber(result.TotalCompressedRecordCount)}");
        Console.WriteLine($"Compressed bytes: {FormatNumber(result.TotalCompressedBytes)}");
        Console.WriteLine($"Decompressed bytes: {FormatNumber(result.TotalDecompressedBytes)}");
        Console.WriteLine($"Published events: {FormatNumber(result.TotalPublishedEvents)}");
        Console.WriteLine($"Valid events: {FormatNumber(result.TotalValidEvents)}");
        Console.WriteLine($"Valid event share: {FormatPercent(result.ValidEventShare)}");
        Console.WriteLine($"Below-threshold events: {FormatNumber(result.TotalBelowThresholdEvents)}");
        Console.WriteLine($"Range-folded events: {FormatNumber(result.TotalRangeFoldedEvents)}");
        Console.WriteLine($"CFP filter-not-applied events: {FormatNumber(result.TotalClutterFilterNotAppliedEvents)}");
        Console.WriteLine($"CFP point-clutter-filter events: {FormatNumber(result.TotalPointClutterFilterAppliedEvents)}");
        Console.WriteLine($"CFP dual-pol-filtered events: {FormatNumber(result.TotalDualPolarizationFilteredEvents)}");
        Console.WriteLine($"Reserved events: {FormatNumber(result.TotalReservedEvents)}");
        Console.WriteLine($"Unsupported events: {FormatNumber(result.TotalUnsupportedEvents)}");
        Console.WriteLine($"Raw value checksum: {FormatNumber(result.TotalRawValueChecksum)}");
        Console.WriteLine($"Calibrated value scaled checksum: {FormatNumber(result.TotalCalibratedValueScaledChecksum)}");
        Console.WriteLine($"Chronology checksum: {FormatUnsignedNumber(result.ChronologyChecksum)}");
        if (result.PublishedFileCount == 0)
        {
            Console.WriteLine("Diagnostic: no Archive Two base-data files were selected for replay.");
        }
    }
}
