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

internal static class ArchiveCliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return RadarPulseCliUsage.Print();
        }

        return args[0] switch
        {
            "list" => await ListArchiveAsync(args[1..]),
            "download" => await DownloadArchiveAsync(args[1..]),
            "inspect" => await ArchiveInspectionCliApplication.RunAsync(args[1..]),
            "replay" => ReplayArchive(args[1..]),
            "stream" => StreamArchive(args[1..]),
            "benchmark" => ArchiveBenchmarkCliApplication.Run(args[1..]),
            "validate" => ArchiveValidationCliApplication.Run(args[1..]),
            _ => RadarPulseCliUsage.Print()
        };
    }

    static async Task<int> ListArchiveAsync(string[] args)
    {
        var options = ArchiveOptions.Parse(args);
        var request = new HistoricalArchiveRequest(
            options.Date ?? throw new InvalidOperationException("--date is required."),
            options.RadarIds,
            options.AllRadars,
            options.MaxFiles,
            options.MaxBytes);

        using var httpClient = new HttpClient();
        IHistoricalArchiveClient client = new AwsNexradArchiveClient(httpClient);
        var manifest = await client.BuildManifestAsync(request, CancellationToken.None);
        if (options.ManifestPath is not null)
        {
            await new HistoricalArchiveManifestWriter().WriteAsync(manifest, options.ManifestPath, CancellationToken.None);
        }

        var summary = manifest.Summarize();

        Console.WriteLine($"Archive date: {manifest.ArchiveDate:yyyy-MM-dd}");
        Console.WriteLine($"Source: AWS {AwsNexradArchiveKey.BucketName}");
        if (options.ManifestPath is not null)
        {
            Console.WriteLine($"Manifest: {options.ManifestPath}");
        }

        Console.WriteLine($"Radars: {FormatNumber(summary.RadarCount)}");
        Console.WriteLine($"Files: {FormatNumber(summary.FileCount)}");
        Console.WriteLine($"Bytes: {FormatNumber(summary.TotalBytes)}");
        foreach (var radar in summary.Radars)
        {
            Console.WriteLine($"{radar.RadarId}: {FormatNumber(radar.FileCount)} files, {FormatNumber(radar.TotalBytes)} bytes");
        }

        return 0;
    }


    static async Task<int> DownloadArchiveAsync(string[] args)
    {
        var options = ArchiveOptions.Parse(args);
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new InvalidOperationException("--output is required.");
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.CancelKeyPress += handler;
        try
        {
            using var httpClient = new HttpClient();
            IHistoricalArchiveClient client = new AwsNexradArchiveClient(httpClient);
            var manifest = await LoadManifestForDownloadAsync(options, client, cancellation.Token);
            var summary = manifest.Summarize();

            Console.WriteLine($"Archive date: {manifest.ArchiveDate:yyyy-MM-dd}");
            Console.WriteLine($"Source: AWS {AwsNexradArchiveKey.BucketName}");
            if (options.ManifestPath is not null)
            {
                Console.WriteLine($"Manifest input: {options.ManifestPath}");
            }

            Console.WriteLine($"Output: {options.OutputPath}");
            Console.WriteLine($"Radars: {FormatNumber(summary.RadarCount)}");
            Console.WriteLine($"Files: {FormatNumber(summary.FileCount)}");
            Console.WriteLine($"Bytes: {FormatNumber(summary.TotalBytes)}");

            var downloader = new HistoricalArchiveDownloader(client, new NexradCachePathMapper());
            var preflight = downloader.CheckPreflight(manifest, options.OutputPath, cancellation.Token);
            Console.WriteLine($"Required download bytes: {FormatNumber(preflight.RequiredDownloadBytes)}");
            Console.WriteLine($"Available disk bytes: {FormatNumber(preflight.AvailableBytes)}");

            var result = await downloader.DownloadAsync(
                manifest,
                options.OutputPath,
                options.Concurrency,
                cancellation.Token);

            Console.WriteLine($"Downloaded files: {FormatNumber(result.DownloadedFileCount)}");
            Console.WriteLine($"Skipped files: {FormatNumber(result.SkippedFileCount)}");
            Console.WriteLine($"Downloaded bytes: {FormatNumber(result.DownloadedBytes)}");
            Console.WriteLine($"Skipped bytes: {FormatNumber(result.SkippedBytes)}");
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }


    static int ReplayArchive(string[] args)
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

    static void PrintArchiveReplayPublishResult(ArchiveReplayPublishResult result)
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

    static int StreamArchive(string[] args)
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

    static void PrintArchiveRadarEventBatchPublishResult(
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

    static void PrintArchiveReplayCachePublishResult(ArchiveReplayCachePublishResult result)
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

    static async Task<HistoricalArchiveManifest> LoadManifestForDownloadAsync(
        ArchiveOptions options,
        IHistoricalArchiveClient client,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            if (options.Date is not null ||
                options.AllRadars)
            {
                throw new InvalidOperationException(
                    "--manifest cannot be combined with --date or --all-radars for download.");
            }

            var manifest = await new HistoricalArchiveManifestReader().ReadAsync(options.ManifestPath, cancellationToken);
            return new HistoricalArchiveManifestSelector().Select(
                manifest,
                options.RadarIds,
                options.MaxFiles,
                options.MaxBytes);
        }

        var request = new HistoricalArchiveRequest(
            options.Date ?? throw new InvalidOperationException("--date is required when --manifest is not provided."),
            options.RadarIds,
            options.AllRadars,
            options.MaxFiles,
            options.MaxBytes);

        return await client.BuildManifestAsync(request, cancellationToken);
    }
}
