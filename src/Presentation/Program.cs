using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

if (args.Length < 2)
{
    PrintUsage();
    return 2;
}

try
{
    return args[0] switch
    {
        "archive" => args[1] switch
        {
            "list" => await ListArchiveAsync(args[2..]),
            "download" => await DownloadArchiveAsync(args[2..]),
            "inspect" => await InspectArchiveAsync(args[2..]),
            "replay" => ReplayArchive(args[2..]),
            "stream" => StreamArchive(args[2..]),
            "benchmark" => BenchmarkArchive(args[2..]),
            "validate" => ValidateArchive(args[2..]),
            _ => PrintUsage()
        },
        "processing" => Processing(args[1..]),
        _ => PrintUsage()
    };
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operation cancelled.");
    return 1;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or IOException or InvalidDataException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
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

static string FormatNumber(long value) => value.ToString("N0").Replace(',', '_');

static string FormatUnsignedNumber(ulong value) => value.ToString("N0").Replace(',', '_');

static string FormatOptionalNumber(long? value) => value.HasValue ? FormatNumber(value.Value) : "none";

static string FormatDecimal(double value) => value.ToString("N2", CultureInfo.InvariantCulture).Replace(',', '_');

static double Ratio(long numerator, long denominator) =>
    denominator <= 0 ? 0 : (double)numerator / denominator;

static string FormatPercent(double value) =>
    (value * 100d).ToString("0.###", CultureInfo.InvariantCulture) + "%";

static double MegabytesPerSecond(long bytes, TimeSpan elapsed) =>
    elapsed.TotalSeconds <= 0
        ? 0
        : bytes / 1_000_000d / elapsed.TotalSeconds;

static double PerSecond(long count, TimeSpan elapsed) =>
    elapsed.TotalSeconds <= 0
        ? 0
        : count / elapsed.TotalSeconds;

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

static int PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  radarpulse archive list --date yyyy-MM-dd --radar KTLX [--max-files n] [--max-bytes n] [--manifest path]");
    Console.WriteLine("  radarpulse archive list --date yyyy-MM-dd --all-radars [--max-files n] [--max-bytes n] [--manifest path]");
    Console.WriteLine("  radarpulse archive download --date yyyy-MM-dd --radar KTLX --output data/nexrad [--concurrency n]");
    Console.WriteLine("  radarpulse archive download --date yyyy-MM-dd --all-radars --output data/nexrad [--concurrency n]");
    Console.WriteLine("  radarpulse archive download --manifest data/manifests/2026-05-04.json --output data/nexrad [--radar KTLX] [--max-files n] [--max-bytes n] [--concurrency n]");
    Console.WriteLine("  radarpulse archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06");
    Console.WriteLine("  radarpulse archive inspect --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n]");
    Console.WriteLine("  radarpulse archive replay --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
    Console.WriteLine("  radarpulse archive replay --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
    Console.WriteLine("  radarpulse archive stream --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
    Console.WriteLine("  radarpulse archive benchmark decompress --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
    Console.WriteLine("  radarpulse archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress] [--decode-moments] [--decode-calibrated-moments]");
    Console.WriteLine("  radarpulse archive benchmark replay-shape --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
    Console.WriteLine("  radarpulse archive benchmark replay-publish --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
    Console.WriteLine("  radarpulse archive benchmark replay-publish --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n] [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
    Console.WriteLine("  radarpulse archive benchmark stream (--file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 | --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n]) [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
    Console.WriteLine("  radarpulse processing benchmark synthetic [--mode sequential|partitioned|async] [--sources n] [--batches n] [--events-per-batch n] [--payload-values n] [--partitions n] [--shards n] [--workers n] [--queue-capacity n] [--handlers none|counter-checksum] [--iterations n] [--warmup-iterations n]");
    Console.WriteLine("  radarpulse processing benchmark rebalance-synthetic [--workload balanced|hot-shard|intrinsic-hot|oscillating|cooldown-storm|quarantine-ttl-retry|quarantine-cooling-clear|quarantine-pressure-change-retry|quarantine-retry-reentry|quarantine-successful-relief-clear|long-no-hot-shard|long-cooldown-rejection|long-unsafe-target-rejection|long-mixed-skipped-reasons|counters-only-retention|all] [--mode static|sampling|rebalance|all] [--execution sync|async] [--workers n] [--queue-capacity n] [--validation-profile off|essential|diagnostic|benchmark] [--quarantine-ttl-evaluations n] [--quarantine-sustained-cooling-samples n] [--quarantine-material-pressure-change n] [--iterations n] [--warmup-iterations n]");
    Console.WriteLine("  radarpulse processing benchmark rebalance-archive (--file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 | --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n]) [--mode static|sampling|rebalance|all] [--provider blocking-borrowed|queued-owned] [--provider-overlap none|producer-consumer] [--retention-strategy snapshot-copy|pooled-copy|builder-transfer] [--execution sync|async] [--workers n] [--queue-capacity n] [--queue-timeout-ms n] [--queue-retained-bytes n] [--queue-telemetry none|summary|recent] [--overlap-telemetry none|summary|recent] [--overlap-consumer-delay-ms n] [--partitions n] [--shards n] [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress] [--validation-profile off|essential|diagnostic|benchmark] [--quarantine-ttl-evaluations n] [--quarantine-sustained-cooling-samples n] [--quarantine-material-pressure-change n] [--retention-mode counters|recent|diagnostic] [--max-retained-decisions n] [--max-retained-transitions n] [--max-retained-accepted-moves n] [--max-retained-validation-failures n] [--skew-profile none|hot-shard|rotating-hot-shard|hot-partition|target-starvation|budget-storm] [--skew-factor n] [--skew-period n]");
    Console.WriteLine("  radarpulse processing benchmark ordered-archive-processing (--file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 | --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n]) [--active-batches n] [--partitions n] [--shards n] [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress] [--queue-telemetry none|summary|recent] [--overlap-telemetry none|summary|recent]");
    Console.WriteLine("    rebalance-archive omitted-provider default: queued-owned + pooled-copy + producer-consumer, async workers 4, queue capacity 8, retained-byte budget 536870912, retained-payload prewarm on.");
    Console.WriteLine("    rebalance-archive fallback/oracle: use --provider blocking-borrowed for the borrowed path and same-run comparison.");
    Console.WriteLine("    rebalance-archive direct MeasureFile()/MeasureCache() defaults use the same queued-owned rollout contour.");
    Console.WriteLine("    ordered-archive-processing uses RunProcessingAsync with ordered active-batch commit over the runtime/archive baseline.");
    Console.WriteLine("    --overlap-consumer-delay-ms is controlled mechanics proof, not natural rollout evidence.");
    Console.WriteLine("  radarpulse archive validate decompress (--file path | --cache data/nexrad [--radar KTLX] [--max-files n])");
    Console.WriteLine("  radarpulse archive validate replay-shape (--file path | --cache data/nexrad [--radar KTLX] [--max-files n]) [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
    return 2;
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

static int BenchmarkArchive(string[] args)
{
    if (args.Length == 0)
    {
        return PrintUsage();
    }

    return args[0] switch
    {
        "decompress" => BenchmarkArchiveDecompression(args[1..]),
        "parse" => BenchmarkArchiveParse(args[1..]),
        "replay-shape" => BenchmarkArchiveReplayShape(args[1..]),
        "replay-publish" => BenchmarkArchiveReplayPublish(args[1..]),
        "stream" => BenchmarkArchiveStream(args[1..]),
        _ => PrintUsage()
    };
}

static int Processing(string[] args)
{
    if (args.Length == 0)
    {
        return PrintUsage();
    }

    return args[0] switch
    {
        "benchmark" => ProcessingBenchmark(args[1..]),
        _ => PrintUsage()
    };
}

static int ProcessingBenchmark(string[] args)
{
    if (args.Length == 0)
    {
        return PrintUsage();
    }

    return args[0] switch
    {
        "synthetic" => BenchmarkProcessingSynthetic(args[1..]),
        "rebalance" or "rebalance-synthetic" => BenchmarkProcessingRebalanceSynthetic(args[1..]),
        "rebalance-archive" => BenchmarkProcessingRebalanceArchive(args[1..]),
        "ordered-archive-processing" => BenchmarkProcessingOrderedArchiveProcessing(args[1..]),
        _ => PrintUsage()
    };
}

static int BenchmarkProcessingSynthetic(string[] args)
{
    var options = ProcessingBenchmarkSyntheticOptions.Parse(args);
    var workloadOptions = new RadarProcessingSyntheticWorkloadOptions(
        options.SourceCount,
        options.BatchCount,
        options.EventsPerBatch,
        options.PayloadValuesPerEvent);
    var result = new RadarProcessingSyntheticBenchmark().Measure(
        workloadOptions,
        options.ExecutionMode,
        options.PartitionCount,
        options.ShardCount,
        options.HandlerSet,
        options.Iterations,
        options.WarmupIterations,
        CancellationToken.None,
        options.AsyncExecution);

    PrintProcessingBenchmarkResult(result);
    return 0;
}

static int BenchmarkProcessingRebalanceSynthetic(string[] args)
{
    var options = ProcessingBenchmarkRebalanceSyntheticOptions.Parse(args);
    var benchmark = new RadarProcessingSyntheticRebalanceBenchmark();
    var printedResult = false;

    foreach (var workloadKind in options.Workloads)
    {
        var workload = RadarProcessingSyntheticRebalanceWorkload.Create(workloadKind);
        var hardeningOptions = CreateProcessingRebalanceSyntheticHardeningOptions(
            workload,
            options.ValidationProfile,
            options.QuarantineLifecycleOverrides);

        foreach (var mode in options.Modes)
        {
            if (printedResult)
            {
                Console.WriteLine();
            }

            var result = benchmark.Measure(
                workload,
                mode,
                options.Iterations,
                options.WarmupIterations,
                CancellationToken.None,
                hardeningOptions,
                options.ExecutionMode,
                options.AsyncExecution);

            PrintProcessingRebalanceBenchmarkResult(result);
            printedResult = true;
        }
    }

    return 0;
}

static RadarProcessingRebalanceHardeningOptions CreateProcessingRebalanceSyntheticHardeningOptions(
    RadarProcessingSyntheticRebalanceWorkload workload,
    RadarProcessingValidationProfile validationProfile,
    ProcessingBenchmarkQuarantineLifecycleOptionOverrides quarantineLifecycleOverrides) =>
    new(
        telemetryRetention: workload.HardeningOptions.TelemetryRetention,
        quarantineLifecycle: quarantineLifecycleOverrides.ApplyTo(workload.HardeningOptions.QuarantineLifecycle),
        validationProfile: validationProfile);

static int BenchmarkProcessingRebalanceArchive(string[] args)
{
    var options = ProcessingBenchmarkArchiveRebalanceOptions.Parse(args);
    var benchmark = new RadarProcessingArchiveRebalanceBenchmark(
        ArchiveBZip2Decompressors.Create(options.Decompressor));
    var hardeningOptions = new RadarProcessingRebalanceHardeningOptions(
        telemetryRetention: options.TelemetryRetention,
        quarantineLifecycle: options.QuarantineLifecycleOverrides.ApplyTo(
            RadarProcessingQuarantineLifecycleOptions.Default),
        validationProfile: options.ValidationProfile);
    var printedResult = false;

    foreach (var mode in options.Modes)
    {
        if (printedResult)
        {
            Console.WriteLine();
        }

        if (options.CachePath is not null)
        {
            var cacheResult = benchmark.MeasureCache(
                options.CachePath,
                options.Date,
                options.RadarId,
                options.MaxFiles,
                mode,
                options.Iterations,
                options.WarmupIterations,
                options.PartitionCount,
                options.ShardCount,
                options.Parallelism,
                CancellationToken.None,
                hardeningOptions,
                options.PressureSkew,
                options.ExecutionMode,
                options.AsyncExecution,
                options.ProviderMode,
                options.ProviderQueueCapacity,
                options.ProviderQueueTimeout,
                options.ProviderOverlapMode,
                options.RetentionStrategy,
                options.ProviderQueueRetainedPayloadBytes,
                options.OverlapConsumerDelay);
            PrintProcessingArchiveRebalanceCacheBenchmarkResult(
                cacheResult,
                options);
        }
        else
        {
            var result = benchmark.MeasureFile(
                options.FilePath ?? throw new InvalidOperationException("--file is required when --cache is not provided."),
                mode,
                options.Iterations,
                options.WarmupIterations,
                options.PartitionCount,
                options.ShardCount,
                options.Parallelism,
                CancellationToken.None,
                hardeningOptions,
                options.PressureSkew,
                options.ExecutionMode,
                options.AsyncExecution,
                options.ProviderMode,
                options.ProviderQueueCapacity,
                options.ProviderQueueTimeout,
                options.ProviderOverlapMode,
                options.RetentionStrategy,
                options.ProviderQueueRetainedPayloadBytes,
                options.OverlapConsumerDelay);
            PrintProcessingArchiveRebalanceBenchmarkResult(
                result,
                options);
        }

        printedResult = true;
    }

    return 0;
}

static int BenchmarkProcessingOrderedArchiveProcessing(string[] args)
{
    var options = ProcessingBenchmarkOrderedArchiveProcessingOptions.Parse(args);
    var benchmark = new RadarProcessingArchiveOrderedProcessingBenchmark(
        ArchiveBZip2Decompressors.Create(options.Decompressor));

    var result = options.CachePath is not null
        ? benchmark.MeasureCache(
            options.CachePath,
            options.Date,
            options.RadarId,
            options.MaxFiles,
            options.Iterations,
            options.WarmupIterations,
            options.PartitionCount,
            options.ShardCount,
            options.Parallelism,
            options.ActiveBatchCapacity,
            CancellationToken.None)
        : benchmark.MeasureFile(
            options.FilePath ?? throw new InvalidOperationException("--file is required when --cache is not provided."),
            options.Iterations,
            options.WarmupIterations,
            options.PartitionCount,
            options.ShardCount,
            options.Parallelism,
            options.ActiveBatchCapacity,
            CancellationToken.None);

    PrintProcessingArchiveOrderedProcessingBenchmarkResult(result, options);
    return 0;
}

static void PrintProcessingBenchmarkResult(RadarProcessingBenchmarkResult result)
{
    Console.WriteLine("Processing benchmark: synthetic");
    Console.WriteLine("Measured contour: RadarProcessingCore over prebuilt RadarEventBatch");
    Console.WriteLine("Excluded work: decompression, Archive Two scanning, identity normalization, batch construction");
    Console.WriteLine($"Execution mode: {FormatProcessingMode(result.ExecutionMode)}");
    Console.WriteLine($"Partitions: {FormatNumber(result.PartitionCount)}");
    Console.WriteLine($"Shards: {FormatNumber(result.ShardCount)}");
    Console.WriteLine($"Validation profile: {FormatProcessingValidationProfile(result.ValidationProfile)}");
    Console.WriteLine($"Handler set: {FormatProcessingHandlerSet(result.HandlerSet)}");
    Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
    Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
    Console.WriteLine($"Source count: {FormatNumber(result.SourceCount)}");
    Console.WriteLine($"Batches per iteration: {FormatNumber(result.BatchesPerIteration)}");
    Console.WriteLine($"Stream events per iteration: {FormatNumber(result.EventsPerIteration)}");
    Console.WriteLine($"Payload values per iteration: {FormatNumber(result.PayloadValuesPerIteration)}");
    Console.WriteLine($"Raw value checksum per iteration: {FormatNumber(result.RawValueChecksumPerIteration)}");
    Console.WriteLine($"Active source count: {FormatNumber(result.ActiveSourceCount)}");
    Console.WriteLine($"Total batches: {FormatNumber(result.TotalBatches)}");
    Console.WriteLine($"Total stream events: {FormatNumber(result.TotalEvents)}");
    Console.WriteLine($"Total payload values: {FormatNumber(result.TotalPayloadValues)}");
    Console.WriteLine($"Validation checksum: {FormatUnsignedNumber(result.ValidationChecksum)}");
    Console.WriteLine($"Elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
    Console.WriteLine($"Batches/s: {FormatDecimal(result.BatchesPerSecond)}");
    Console.WriteLine($"Stream events/s: {FormatDecimal(result.EventsPerSecond)}");
    Console.WriteLine($"Payload values/s: {FormatDecimal(result.PayloadValuesPerSecond)}");
    Console.WriteLine($"Allocated bytes: {FormatNumber(result.AllocatedBytes)}");
    Console.WriteLine($"Allocated bytes / stream event: {FormatDecimal(result.AllocatedBytesPerEvent)}");
    Console.WriteLine($"Allocated bytes / payload value: {FormatDecimal(result.AllocatedBytesPerPayloadValue)}");
    if (result.WorkerTelemetry is not null)
    {
        PrintProcessingWorkerTelemetry(result.WorkerTelemetry);
    }

    if (result.AsyncValidation is not null)
    {
        Console.WriteLine($"Async validation: {FormatBoolean(result.AsyncValidation.IsValid)}");
        if (result.AsyncValidation.HasComparisonChecksums)
        {
            Console.WriteLine($"Sync comparison checksum: {FormatUnsignedNumber(result.AsyncValidation.SynchronousChecksum!.Value)}");
            Console.WriteLine($"Async comparison checksum: {FormatUnsignedNumber(result.AsyncValidation.AsyncChecksum!.Value)}");
        }
    }

    foreach (var shard in result.ShardDistributions)
    {
        Console.WriteLine($"Shard {FormatNumber(shard.ShardId)} events per iteration: {FormatNumber(shard.EventCount)}");
    }
}

static void PrintProcessingWorkerTelemetry(RadarProcessingWorkerTelemetrySummary workerTelemetry)
{
    Console.WriteLine($"Worker count: {FormatNumber(workerTelemetry.WorkerCount)}");
    Console.WriteLine($"Worker queue capacity: {FormatNumber(workerTelemetry.QueueCapacity)}");
    Console.WriteLine($"Worker dispatched batches: {FormatNumber(workerTelemetry.Counters.DispatchedBatchCount)}");
    Console.WriteLine($"Worker completed batches: {FormatNumber(workerTelemetry.Counters.CompletedBatchCount)}");
    Console.WriteLine($"Worker failed batches: {FormatNumber(workerTelemetry.Counters.FailedBatchCount)}");
    Console.WriteLine($"Worker submitted items: {FormatNumber(workerTelemetry.Counters.SubmittedWorkItemCount)}");
    Console.WriteLine($"Worker accepted items: {FormatNumber(workerTelemetry.Counters.AcceptedWorkItemCount)}");
    Console.WriteLine($"Worker completed items: {FormatNumber(workerTelemetry.Counters.CompletedWorkItemCount)}");
    Console.WriteLine($"Worker succeeded items: {FormatNumber(workerTelemetry.Counters.SucceededWorkItemCount)}");
    Console.WriteLine($"Worker failed items: {FormatNumber(workerTelemetry.Counters.FailedWorkItemCount)}");
    Console.WriteLine($"Worker dispatch ms: {FormatDecimal(workerTelemetry.Counters.TotalDispatchTime.TotalMilliseconds)}");
    Console.WriteLine($"Worker queue wait ms: {FormatDecimal(workerTelemetry.Counters.TotalQueueWaitTime.TotalMilliseconds)}");
    Console.WriteLine($"Worker execution ms: {FormatDecimal(workerTelemetry.Counters.TotalExecutionTime.TotalMilliseconds)}");
    Console.WriteLine($"Worker aggregation ms: {FormatDecimal(workerTelemetry.Counters.TotalAggregationTime.TotalMilliseconds)}");
    Console.WriteLine($"Worker barrier wait ms: {FormatDecimal(workerTelemetry.Counters.TotalBarrierWaitTime.TotalMilliseconds)}");
}

static void PrintProcessingRebalanceBenchmarkResult(RadarProcessingSyntheticRebalanceBenchmarkResult result)
{
    Console.WriteLine("Processing benchmark: rebalance-synthetic");
    Console.WriteLine("Measured contour: RadarProcessingCore plus rebalance evaluation over prebuilt synthetic RadarEventBatch values");
    Console.WriteLine("Excluded work: decompression, Archive Two scanning, identity normalization, batch construction, CLI formatting");
    Console.WriteLine($"Execution mode: {FormatProcessingMode(result.ExecutionMode)}");
    Console.WriteLine($"Workload: {FormatProcessingRebalanceWorkload(result.WorkloadKind)}");
    Console.WriteLine($"Benchmark mode: {FormatProcessingRebalanceMode(result.Mode)}");
    Console.WriteLine($"Validation profile: {FormatProcessingValidationProfile(result.ValidationProfile)}");
    Console.WriteLine($"Telemetry retention mode: {FormatProcessingRetentionMode(result.RetentionMode)}");
    PrintProcessingQuarantineLifecycle(
        result.QuarantineTtlEvaluations,
        result.QuarantineSustainedCoolingSampleCount,
        result.QuarantineMaterialPressureChangeThreshold);
    Console.WriteLine($"Partitions: {FormatNumber(result.PartitionCount)}");
    Console.WriteLine($"Shards: {FormatNumber(result.ShardCount)}");
    Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
    Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
    Console.WriteLine($"Source count: {FormatNumber(result.SourceCount)}");
    Console.WriteLine($"Batches per iteration: {FormatNumber(result.BatchesPerIteration)}");
    Console.WriteLine($"Stream events per iteration: {FormatNumber(result.EventsPerIteration)}");
    Console.WriteLine($"Payload values per iteration: {FormatNumber(result.PayloadValuesPerIteration)}");
    Console.WriteLine($"Raw value checksum per iteration: {FormatNumber(result.RawValueChecksumPerIteration)}");
    Console.WriteLine($"Topology versions per iteration: {FormatNumber(result.TopologyVersionCount)}");
    Console.WriteLine($"Rebalance evaluations: {FormatNumber(result.RebalanceEvaluationCount)}");
    Console.WriteLine($"Accepted moves: {FormatNumber(result.AcceptedMoveCount)}");
    Console.WriteLine($"Skipped decisions: {FormatNumber(result.SkippedDecisionCount)}");
    Console.WriteLine($"Direct hot relief moves: {FormatNumber(result.DirectHotReliefCount)}");
    Console.WriteLine($"Cold evacuation moves: {FormatNumber(result.ColdEvacuationCount)}");
    Console.WriteLine($"Failed migrations: {FormatNumber(result.FailedMigrationCount)}");
    Console.WriteLine($"Validation: {(result.ValidationSucceeded ? "succeeded" : "failed")}");
    Console.WriteLine($"Validation checksum: {FormatUnsignedNumber(result.ValidationChecksum)}");
    Console.WriteLine($"Skipped reasons: {FormatProcessingRebalanceSkippedReasons(result.SkippedReasons)}");
    Console.WriteLine($"Elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
    Console.WriteLine($"Batches/s: {FormatDecimal(result.BatchesPerSecond)}");
    Console.WriteLine($"Stream events/s: {FormatDecimal(result.EventsPerSecond)}");
    Console.WriteLine($"Payload values/s: {FormatDecimal(result.PayloadValuesPerSecond)}");
    Console.WriteLine($"Rebalance evaluations/s: {FormatDecimal(result.RebalanceEvaluationsPerSecond)}");
    Console.WriteLine($"Allocated bytes: {FormatNumber(result.AllocatedBytes)}");
    Console.WriteLine($"Allocation includes CLI formatting: {FormatBoolean(result.AllocationSummary.IncludesCliFormatting)}");
    Console.WriteLine($"Allocated bytes / stream event: {FormatDecimal(result.AllocatedBytesPerStreamEvent)}");
    Console.WriteLine($"Allocated bytes / payload value: {FormatDecimal(result.AllocatedBytesPerPayloadValue)}");
    Console.WriteLine($"Allocated bytes / rebalance evaluation: {FormatDecimal(result.AllocatedBytesPerRebalanceEvaluation)}");
    if (result.WorkerTelemetry is not null)
    {
        PrintProcessingWorkerTelemetry(result.WorkerTelemetry);
    }

    PrintProcessingRebalanceMovePressures(result.AcceptedMovePressures);
}

static void PrintProcessingArchiveRebalanceBenchmarkResult(
    RadarProcessingArchiveRebalanceBenchmarkResult result,
    ProcessingBenchmarkArchiveRebalanceOptions options)
{
    var queueTelemetryOutput = options.QueueTelemetryOutput;
    var overlapTelemetryOutput = options.OverlapTelemetryOutput;
    Console.WriteLine("Processing benchmark: rebalance-archive");
    Console.WriteLine("Measured contour: Archive replay to RadarEventBatch plus processing rebalance callback");
    Console.WriteLine("Processing-only timing: RadarEventBatch callback inside archive publisher");
    Console.WriteLine(result.ProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned
        ? "Batch lifetime: leased batches are converted to owned snapshots before provider queue enqueue"
        : "Batch lifetime: leased batches are processed during the callback and are not retained");
    Console.WriteLine($"File: {result.FilePath}");
    Console.WriteLine($"Decompressor: {result.Decompressor}");
    Console.WriteLine($"Archive parallelism: {FormatNumber(result.DegreeOfParallelism)}");
    Console.WriteLine($"Provider mode: {FormatProcessingArchiveProviderMode(result.ProviderMode)}");
    Console.WriteLine($"Provider queue capacity: {FormatNumber(result.QueueCapacity)}");
    Console.WriteLine($"Provider overlap mode: {FormatProcessingProviderOverlapMode(result.ProviderOverlapMode)}");
    Console.WriteLine($"Provider overlap consumer delay ms: {FormatDecimal(result.OverlapConsumerDelay.TotalMilliseconds)}");
    Console.WriteLine($"Retention strategy: {FormatProcessingRetentionStrategy(result.RetentionStrategy)}");
    Console.WriteLine($"Provider queue retained byte capacity: {FormatOptionalNumber(result.QueueRetainedPayloadBytes)}");
    PrintProcessingRetainedPayloadPrewarm(result.RetainedPayloadPrewarm);
    var providerOverlapEvidenceContour =
        FormatProviderOverlapEvidenceContourForFileBenchmark(result, queueTelemetryOutput, overlapTelemetryOutput);
    var isDefaultCandidateContour =
        IsDefaultCandidateFileBenchmarkContour(result, queueTelemetryOutput, overlapTelemetryOutput);
    PrintProcessingArchiveRebalanceProviderSelection(
        result.ProviderMode,
        result.ProviderOverlapMode,
        result.ExecutionMode,
        options.EffectiveOptionProvenance,
        isDefaultCandidateContour,
        options.IsRolloutDefaultExpandedContour,
        options.IsExplicitBlockingBorrowedFallback);
    Console.WriteLine($"Default-candidate contour: {FormatBoolean(isDefaultCandidateContour)}");
    Console.WriteLine($"Provider overlap evidence contour: {providerOverlapEvidenceContour}");
    Console.WriteLine($"Provider overlap evidence scope: {FormatProviderOverlapEvidenceScope(providerOverlapEvidenceContour)}");
    Console.WriteLine($"Execution mode: {FormatProcessingMode(result.ExecutionMode)}");
    Console.WriteLine($"Benchmark mode: {FormatProcessingRebalanceMode(result.Mode)}");
    Console.WriteLine($"Validation profile: {FormatProcessingValidationProfile(result.ValidationProfile)}");
    Console.WriteLine($"Telemetry retention mode: {FormatProcessingRetentionMode(result.RetentionMode)}");
    PrintProcessingQuarantineLifecycle(
        result.QuarantineTtlEvaluations,
        result.QuarantineSustainedCoolingSampleCount,
        result.QuarantineMaterialPressureChangeThreshold);
    Console.WriteLine($"Max retained decisions: {FormatNumber(result.MaxRetainedDecisions)}");
    Console.WriteLine($"Max retained lifecycle transitions: {FormatNumber(result.MaxRetainedLifecycleTransitions)}");
    Console.WriteLine($"Max retained accepted moves: {FormatNumber(result.MaxRetainedAcceptedMoves)}");
    Console.WriteLine($"Max retained validation failures: {FormatNumber(result.MaxRetainedValidationFailures)}");
    PrintProcessingPressureSkew(result.PressureSkew);
    Console.WriteLine($"Source count: {FormatNumber(result.SourceCount)}");
    Console.WriteLine($"Partitions: {FormatNumber(result.PartitionCount)}");
    Console.WriteLine($"Shards: {FormatNumber(result.ShardCount)}");
    Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
    Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
    Console.WriteLine($"File size bytes per iteration: {FormatNumber(result.FileSizeBytesPerIteration)}");
    Console.WriteLine($"Compressed records per iteration: {FormatNumber(result.CompressedRecordsPerIteration)}");
    Console.WriteLine($"Compressed bytes per iteration: {FormatNumber(result.CompressedBytesPerIteration)}");
    Console.WriteLine($"Decompressed bytes per iteration: {FormatNumber(result.DecompressedBytesPerIteration)}");
    Console.WriteLine($"Batches per iteration: {FormatNumber(result.BatchesPerIteration)}");
    Console.WriteLine($"Stream events per iteration: {FormatNumber(result.EventsPerIteration)}");
    Console.WriteLine($"Payload bytes per iteration: {FormatNumber(result.PayloadBytesPerIteration)}");
    Console.WriteLine($"Payload values per iteration: {FormatNumber(result.PayloadValuesPerIteration)}");
    Console.WriteLine($"Raw value checksum per iteration: {FormatNumber(result.RawValueChecksumPerIteration)}");
    Console.WriteLine($"Topology versions per iteration: {FormatNumber(result.TopologyVersionCount)}");
    Console.WriteLine($"Rebalance evaluations: {FormatNumber(result.RebalanceEvaluationCount)}");
    Console.WriteLine($"Accepted moves: {FormatNumber(result.AcceptedMoveCount)}");
    Console.WriteLine($"Skipped decisions: {FormatNumber(result.SkippedDecisionCount)}");
    Console.WriteLine($"Direct hot relief moves: {FormatNumber(result.DirectHotReliefCount)}");
    Console.WriteLine($"Cold evacuation moves: {FormatNumber(result.ColdEvacuationCount)}");
    Console.WriteLine($"Failed migrations: {FormatNumber(result.FailedMigrationCount)}");
    Console.WriteLine($"Validation: {(result.ValidationSucceeded ? "succeeded" : "failed")}");
    Console.WriteLine($"Processing completeness: {(result.ProcessingSucceeded ? "succeeded" : "failed")}");
    Console.WriteLine($"Processing validation failed batches: {FormatNumber(result.ProcessingValidationFailedBatchCount)}");
    Console.WriteLine($"Validation checksum: {FormatUnsignedNumber(result.ValidationChecksum)}");
    Console.WriteLine($"Skipped reasons: {FormatProcessingRebalanceSkippedReasons(result.SkippedReasons)}");
    Console.WriteLine($"Skipped reason counters: {FormatProcessingRebalanceSkippedReasonCounters(result.SkippedReasonCounters)}");
    PrintProcessingRebalanceRetentionStats(result.RetentionStats);
    Console.WriteLine($"End-to-end elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
    Console.WriteLine($"Processing callback elapsed ms: {FormatDecimal(result.ProcessingElapsed.TotalMilliseconds)}");
    Console.WriteLine($"Replay and batch construction elapsed ms: {FormatDecimal(result.ReplayAndBatchConstructionElapsed.TotalMilliseconds)}");
    PrintProcessingProviderQueueTelemetryForArchiveFile(result, queueTelemetryOutput);
    PrintProcessingProviderRetentionTelemetryForArchiveFile(result);
    PrintProcessingProviderOverlapTelemetryForArchiveFile(result, overlapTelemetryOutput);
    Console.WriteLine($"Compressed MB/s: {FormatDecimal(result.CompressedMegabytesPerSecond)}");
    Console.WriteLine($"Decompressed MB/s: {FormatDecimal(result.DecompressedMegabytesPerSecond)}");
    Console.WriteLine($"End-to-end stream events/s: {FormatDecimal(result.EventsPerSecond)}");
    Console.WriteLine($"End-to-end payload values/s: {FormatDecimal(result.PayloadValuesPerSecond)}");
    Console.WriteLine($"Processing stream events/s: {FormatDecimal(result.ProcessingEventsPerSecond)}");
    Console.WriteLine($"Processing payload values/s: {FormatDecimal(result.ProcessingPayloadValuesPerSecond)}");
    Console.WriteLine($"Rebalance evaluations/s: {FormatDecimal(result.RebalanceEvaluationsPerSecond)}");
    Console.WriteLine($"End-to-end allocated bytes: {FormatNumber(result.AllocatedBytes)}");
    Console.WriteLine($"Processing callback allocated bytes: {FormatNumber(result.ProcessingCallbackAllocatedBytes)}");
    Console.WriteLine($"Replay and batch construction allocated bytes: {FormatNumber(result.ReplayAndBatchConstructionAllocatedBytes)}");
    PrintProcessingArchiveRebalanceAllocationAttribution(
        result.AllocationSummary,
        result.TotalPayloadValues);
    Console.WriteLine($"End-to-end allocated bytes / stream event: {FormatDecimal(result.AllocatedBytesPerStreamEvent)}");
    Console.WriteLine($"End-to-end allocated bytes / payload value: {FormatDecimal(result.AllocatedBytesPerPayloadValue)}");
    Console.WriteLine($"Processing callback allocated bytes / payload value: {FormatDecimal(result.ProcessingCallbackAllocatedBytesPerPayloadValue)}");
    Console.WriteLine($"Processing callback allocated bytes / rebalance evaluation: {FormatDecimal(result.ProcessingCallbackAllocatedBytesPerRebalanceEvaluation)}");
    Console.WriteLine($"Replay and batch construction allocated bytes / payload value: {FormatDecimal(result.ReplayAndBatchConstructionAllocatedBytesPerPayloadValue)}");
    if (result.WorkerTelemetry is not null)
    {
        PrintProcessingWorkerTelemetry(result.WorkerTelemetry);
    }

    PrintProcessingRebalanceMovePressures(result.AcceptedMovePressures);
}

static void PrintProcessingArchiveRebalanceProviderSelection(
    RadarProcessingArchiveProviderMode providerMode,
    RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
    RadarProcessingExecutionMode executionMode,
    ProcessingBenchmarkArchiveRebalanceOptionProvenance provenance,
    bool isDefaultCandidateContour,
    bool isRolloutDefaultExpandedContour,
    bool isExplicitBlockingBorrowedFallback)
{
    var isQueuedOwned = providerMode == RadarProcessingArchiveProviderMode.QueuedOwned;
    var hasProducerConsumerOverlap =
        isQueuedOwned &&
        providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer;
    var isAsyncExecution = executionMode == RadarProcessingExecutionMode.AsyncShardTransport;

    Console.WriteLine($"Provider mode source: {FormatProcessingBenchmarkOptionValueSource(provenance.ProviderMode)}");
    Console.WriteLine($"Provider overlap source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.ProviderOverlapMode, isQueuedOwned)}");
    Console.WriteLine($"Retention strategy source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.RetentionStrategy, isQueuedOwned)}");
    Console.WriteLine($"Provider queue capacity source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueCapacity, isQueuedOwned)}");
    Console.WriteLine($"Worker queue capacity source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueCapacity, isAsyncExecution)}");
    Console.WriteLine($"Provider queue retained byte capacity source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueRetainedPayloadBytes, isQueuedOwned)}");
    Console.WriteLine($"Queue telemetry source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueTelemetry, isQueuedOwned)}");
    Console.WriteLine($"Provider overlap telemetry source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.OverlapTelemetry, hasProducerConsumerOverlap)}");
    Console.WriteLine($"Provider overlap consumer delay source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.OverlapConsumerDelay, hasProducerConsumerOverlap)}");
    Console.WriteLine($"Execution mode source: {FormatProcessingBenchmarkOptionValueSource(provenance.ExecutionMode)}");
    Console.WriteLine($"Worker count source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.WorkerCount, isAsyncExecution)}");
    Console.WriteLine($"Provider default rollout contour: {FormatBoolean(isDefaultCandidateContour)}");
    Console.WriteLine($"Provider rollout default expansion: {FormatBoolean(isRolloutDefaultExpandedContour)}");
    Console.WriteLine($"Provider fallback contour: {FormatBoolean(isExplicitBlockingBorrowedFallback)}");
}

static void PrintProcessingArchiveRebalanceAllocationAttribution(
    RadarProcessingRebalanceAllocationSummary allocation,
    long payloadValueCount)
{
    Console.WriteLine("Allocation attribution: summary");
    Console.WriteLine("Allocation measured counter scope: global");
    Console.WriteLine("Allocation processing callback counter scope: global");
    Console.WriteLine($"Allocation measured bytes: {FormatNumber(allocation.MeasuredAllocatedBytes)}");
    Console.WriteLine($"Allocation processing callback bytes: {FormatNumber(allocation.ProcessingCallbackAllocatedBytes)}");
    Console.WriteLine($"Allocation replay and batch construction bytes: {FormatNumber(allocation.ReplayAndBatchConstructionAllocatedBytes)}");
    Console.WriteLine($"Allocation owned snapshot bytes: {FormatNumber(allocation.OwnedSnapshotAllocatedBytes)}");
    Console.WriteLine($"Allocation processing callback non-owned snapshot bytes: {FormatNumber(allocation.ProcessingCallbackNonOwnedSnapshotAllocatedBytes)}");
    Console.WriteLine($"Allocation includes archive replay and batch construction: {FormatBoolean(allocation.IncludesArchiveReplayAndBatchConstruction)}");
    Console.WriteLine($"Allocation includes CLI formatting: {FormatBoolean(allocation.IncludesCliFormatting)}");
    Console.WriteLine($"Allocation owned snapshot bytes / payload value: {FormatDecimal(allocation.OwnedSnapshotAllocatedBytesPerPayloadValue(payloadValueCount))}");
    Console.WriteLine($"Allocation processing callback non-owned snapshot bytes / payload value: {FormatDecimal(allocation.ProcessingCallbackNonOwnedSnapshotAllocatedBytesPerPayloadValue(payloadValueCount))}");
}

static void PrintProcessingRetainedPayloadPrewarm(
    RadarProcessingRetainedPayloadPrewarmResult prewarm)
{
    Console.WriteLine($"Retained payload prewarm: {FormatBoolean(prewarm.Applied)}");
    if (!prewarm.Applied)
    {
        return;
    }

    Console.WriteLine($"Retained payload prewarm event count: {FormatNumber(prewarm.EventCount)}");
    Console.WriteLine($"Retained payload prewarm payload bytes: {FormatNumber(prewarm.PayloadBytes)}");
    Console.WriteLine($"Retained payload prewarm batch count: {FormatNumber(prewarm.RetainedBatchCount)}");
    Console.WriteLine($"Retained payload prewarm elapsed ms: {FormatDecimal(prewarm.Elapsed.TotalMilliseconds)}");
    Console.WriteLine($"Retained payload prewarm allocated bytes: {FormatNumber(prewarm.AllocatedBytes)}");
    Console.WriteLine($"Retained payload prewarm retained bytes: {FormatNumber(prewarm.RetainedBytes)}");
}

static void PrintProcessingArchiveRebalanceCacheBenchmarkResult(
    RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
    ProcessingBenchmarkArchiveRebalanceOptions options)
{
    var queueTelemetryOutput = options.QueueTelemetryOutput;
    var overlapTelemetryOutput = options.OverlapTelemetryOutput;
    Console.WriteLine("Processing benchmark: rebalance-archive cache");
    Console.WriteLine("Measured contour: Archive cache replay to RadarEventBatch plus processing rebalance callback");
    Console.WriteLine("Processing-only timing: RadarEventBatch callback inside archive publisher");
    Console.WriteLine(result.ProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned
        ? "Batch lifetime: leased batches are converted to owned snapshots before provider queue enqueue"
        : "Batch lifetime: leased batches are processed during the callback and are not retained");
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
    Console.WriteLine($"Archive parallelism: {FormatNumber(result.DegreeOfParallelism)}");
    Console.WriteLine($"Provider mode: {FormatProcessingArchiveProviderMode(result.ProviderMode)}");
    Console.WriteLine($"Provider queue capacity: {FormatNumber(result.QueueCapacity)}");
    Console.WriteLine($"Provider overlap mode: {FormatProcessingProviderOverlapMode(result.ProviderOverlapMode)}");
    Console.WriteLine($"Provider overlap consumer delay ms: {FormatDecimal(result.OverlapConsumerDelay.TotalMilliseconds)}");
    Console.WriteLine($"Retention strategy: {FormatProcessingRetentionStrategy(result.RetentionStrategy)}");
    Console.WriteLine($"Provider queue retained byte capacity: {FormatOptionalNumber(result.QueueRetainedPayloadBytes)}");
    PrintProcessingRetainedPayloadPrewarm(result.RetainedPayloadPrewarm);
    var providerOverlapEvidenceContour =
        FormatProviderOverlapEvidenceContourForCacheBenchmark(result, queueTelemetryOutput, overlapTelemetryOutput);
    var isDefaultCandidateContour =
        IsDefaultCandidateCacheBenchmarkContour(result, queueTelemetryOutput, overlapTelemetryOutput);
    PrintProcessingArchiveRebalanceProviderSelection(
        result.ProviderMode,
        result.ProviderOverlapMode,
        result.ExecutionMode,
        options.EffectiveOptionProvenance,
        isDefaultCandidateContour,
        options.IsRolloutDefaultExpandedContour,
        options.IsExplicitBlockingBorrowedFallback);
    Console.WriteLine($"Default-candidate contour: {FormatBoolean(isDefaultCandidateContour)}");
    Console.WriteLine($"Provider overlap evidence contour: {providerOverlapEvidenceContour}");
    Console.WriteLine($"Provider overlap evidence scope: {FormatProviderOverlapEvidenceScope(providerOverlapEvidenceContour)}");
    Console.WriteLine($"Execution mode: {FormatProcessingMode(result.ExecutionMode)}");
    Console.WriteLine($"Benchmark mode: {FormatProcessingRebalanceMode(result.Mode)}");
    Console.WriteLine($"Validation profile: {FormatProcessingValidationProfile(result.ValidationProfile)}");
    Console.WriteLine($"Telemetry retention mode: {FormatProcessingRetentionMode(result.RetentionMode)}");
    PrintProcessingQuarantineLifecycle(
        result.QuarantineTtlEvaluations,
        result.QuarantineSustainedCoolingSampleCount,
        result.QuarantineMaterialPressureChangeThreshold);
    Console.WriteLine($"Max retained decisions: {FormatNumber(result.MaxRetainedDecisions)}");
    Console.WriteLine($"Max retained lifecycle transitions: {FormatNumber(result.MaxRetainedLifecycleTransitions)}");
    Console.WriteLine($"Max retained accepted moves: {FormatNumber(result.MaxRetainedAcceptedMoves)}");
    Console.WriteLine($"Max retained validation failures: {FormatNumber(result.MaxRetainedValidationFailures)}");
    PrintProcessingPressureSkew(result.PressureSkew);
    Console.WriteLine($"Source count: {FormatNumber(result.SourceCount)}");
    Console.WriteLine($"Partitions: {FormatNumber(result.PartitionCount)}");
    Console.WriteLine($"Shards: {FormatNumber(result.ShardCount)}");
    Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
    Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
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
    Console.WriteLine($"Topology versions per iteration: {FormatNumber(result.TopologyVersionCount)}");
    Console.WriteLine($"Rebalance evaluations: {FormatNumber(result.RebalanceEvaluationCount)}");
    Console.WriteLine($"Accepted moves: {FormatNumber(result.AcceptedMoveCount)}");
    Console.WriteLine($"Skipped decisions: {FormatNumber(result.SkippedDecisionCount)}");
    Console.WriteLine($"Direct hot relief moves: {FormatNumber(result.DirectHotReliefCount)}");
    Console.WriteLine($"Cold evacuation moves: {FormatNumber(result.ColdEvacuationCount)}");
    Console.WriteLine($"Failed migrations: {FormatNumber(result.FailedMigrationCount)}");
    Console.WriteLine($"Validation: {(result.ValidationSucceeded ? "succeeded" : "failed")}");
    Console.WriteLine($"Processing completeness: {(result.ProcessingSucceeded ? "succeeded" : "failed")}");
    Console.WriteLine($"Processing validation failed batches: {FormatNumber(result.ProcessingValidationFailedBatchCount)}");
    Console.WriteLine($"Validation checksum: {FormatUnsignedNumber(result.ValidationChecksum)}");
    Console.WriteLine($"Skipped reasons: {FormatProcessingRebalanceSkippedReasons(result.SkippedReasons)}");
    Console.WriteLine($"Skipped reason counters: {FormatProcessingRebalanceSkippedReasonCounters(result.SkippedReasonCounters)}");
    PrintProcessingRebalanceRetentionStats(result.RetentionStats);
    Console.WriteLine($"End-to-end elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
    Console.WriteLine($"Processing callback elapsed ms: {FormatDecimal(result.ProcessingElapsed.TotalMilliseconds)}");
    Console.WriteLine($"Replay and batch construction elapsed ms: {FormatDecimal(result.ReplayAndBatchConstructionElapsed.TotalMilliseconds)}");
    PrintProcessingProviderQueueTelemetryForArchiveCache(result, queueTelemetryOutput);
    PrintProcessingProviderRetentionTelemetryForArchiveCache(result);
    PrintProcessingProviderOverlapTelemetryForArchiveCache(result, overlapTelemetryOutput);
    Console.WriteLine($"Compressed MB/s: {FormatDecimal(result.CompressedMegabytesPerSecond)}");
    Console.WriteLine($"Decompressed MB/s: {FormatDecimal(result.DecompressedMegabytesPerSecond)}");
    Console.WriteLine($"Files/s: {FormatDecimal(result.FilesPerSecond)}");
    Console.WriteLine($"End-to-end stream events/s: {FormatDecimal(result.EventsPerSecond)}");
    Console.WriteLine($"End-to-end payload values/s: {FormatDecimal(result.PayloadValuesPerSecond)}");
    Console.WriteLine($"Processing stream events/s: {FormatDecimal(result.ProcessingEventsPerSecond)}");
    Console.WriteLine($"Processing payload values/s: {FormatDecimal(result.ProcessingPayloadValuesPerSecond)}");
    Console.WriteLine($"Rebalance evaluations/s: {FormatDecimal(result.RebalanceEvaluationsPerSecond)}");
    Console.WriteLine($"End-to-end allocated bytes: {FormatNumber(result.AllocatedBytes)}");
    Console.WriteLine($"Processing callback allocated bytes: {FormatNumber(result.ProcessingCallbackAllocatedBytes)}");
    Console.WriteLine($"Replay and batch construction allocated bytes: {FormatNumber(result.ReplayAndBatchConstructionAllocatedBytes)}");
    PrintProcessingArchiveRebalanceAllocationAttribution(
        result.AllocationSummary,
        result.TotalPayloadValues);
    Console.WriteLine($"End-to-end allocated bytes / stream event: {FormatDecimal(result.AllocatedBytesPerStreamEvent)}");
    Console.WriteLine($"End-to-end allocated bytes / payload value: {FormatDecimal(result.AllocatedBytesPerPayloadValue)}");
    Console.WriteLine($"Processing callback allocated bytes / payload value: {FormatDecimal(result.ProcessingCallbackAllocatedBytesPerPayloadValue)}");
    Console.WriteLine($"Processing callback allocated bytes / rebalance evaluation: {FormatDecimal(result.ProcessingCallbackAllocatedBytesPerRebalanceEvaluation)}");
    Console.WriteLine($"Replay and batch construction allocated bytes / payload value: {FormatDecimal(result.ReplayAndBatchConstructionAllocatedBytesPerPayloadValue)}");
    if (result.WorkerTelemetry is not null)
    {
        PrintProcessingWorkerTelemetry(result.WorkerTelemetry);
    }

    PrintProcessingRebalanceMovePressures(result.AcceptedMovePressures);
}

static void PrintProcessingArchiveOrderedProcessingBenchmarkResult(
    RadarProcessingArchiveOrderedProcessingBenchmarkResult result,
    ProcessingBenchmarkOrderedArchiveProcessingOptions options)
{
    Console.WriteLine(result.IsCache
        ? "Processing benchmark: ordered-archive-processing cache"
        : "Processing benchmark: ordered-archive-processing");
    Console.WriteLine("Measured contour: Archive replay to RadarEventBatch through RunProcessingAsync ordered active-batch drain");
    Console.WriteLine("Processing-only timing: provider/consumer overlap result around ordered processing drain");
    Console.WriteLine("Batch lifetime: leased batches are converted to owned snapshots before provider queue enqueue");
    if (result.IsCache)
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
    }
    else
    {
        Console.WriteLine($"File: {result.FilePath}");
    }

    Console.WriteLine($"Decompressor: {result.Decompressor}");
    Console.WriteLine($"Archive parallelism: {FormatNumber(result.DegreeOfParallelism)}");
    Console.WriteLine($"Provider mode: {FormatProcessingArchiveProviderMode(RadarProcessingArchiveProviderMode.QueuedOwned)}");
    Console.WriteLine($"Provider queue capacity: {FormatNumber(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity)}");
    Console.WriteLine($"Provider overlap mode: {FormatProcessingProviderOverlapMode(RadarProcessingQueuedProviderOverlapMode.ProducerConsumer)}");
    Console.WriteLine($"Retention strategy: {FormatProcessingRetentionStrategy(RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy)}");
    Console.WriteLine($"Provider queue retained byte capacity: {FormatNumber(RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes)}");
    PrintProcessingRetainedPayloadPrewarm(result.RetainedPayloadPrewarm);
    Console.WriteLine("Provider mode source: runtime-archive-baseline");
    Console.WriteLine("Provider overlap source: runtime-archive-baseline");
    Console.WriteLine("Retention strategy source: runtime-archive-baseline");
    Console.WriteLine("Provider queue capacity source: runtime-archive-baseline");
    Console.WriteLine("Provider queue retained byte capacity source: runtime-archive-baseline");
    Console.WriteLine("Execution mode source: runtime-archive-baseline");
    Console.WriteLine("Worker count source: runtime-archive-baseline");
    Console.WriteLine("Ordered active batch capacity source: explicit-or-baseline");
    Console.WriteLine($"Execution mode: {FormatProcessingMode(RadarProcessingRuntimeArchiveBaseline.ExecutionMode)}");
    Console.WriteLine($"Ordered active batch capacity: {FormatNumber(result.ActiveBatchCapacity)}");
    Console.WriteLine($"Source count: {FormatNumber(result.SourceCount)}");
    Console.WriteLine($"Partitions: {FormatNumber(result.PartitionCount)}");
    Console.WriteLine($"Shards: {FormatNumber(result.ShardCount)}");
    Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
    Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
    if (result.IsCache)
    {
        Console.WriteLine($"Examined files per iteration: {FormatNumber(result.ExaminedFilesPerIteration)}");
        Console.WriteLine($"Skipped files per iteration: {FormatNumber(result.SkippedFilesPerIteration)}");
        Console.WriteLine($"Published files per iteration: {FormatNumber(result.PublishedFilesPerIteration)}");
    }

    Console.WriteLine($"File size bytes per iteration: {FormatNumber(result.FileSizeBytesPerIteration)}");
    Console.WriteLine($"Compressed records per iteration: {FormatNumber(result.CompressedRecordsPerIteration)}");
    Console.WriteLine($"Compressed bytes per iteration: {FormatNumber(result.CompressedBytesPerIteration)}");
    Console.WriteLine($"Decompressed bytes per iteration: {FormatNumber(result.DecompressedBytesPerIteration)}");
    Console.WriteLine($"Batches per iteration: {FormatNumber(result.BatchesPerIteration)}");
    Console.WriteLine($"Stream events per iteration: {FormatNumber(result.EventsPerIteration)}");
    Console.WriteLine($"Payload bytes per iteration: {FormatNumber(result.PayloadBytesPerIteration)}");
    Console.WriteLine($"Payload values per iteration: {FormatNumber(result.PayloadValuesPerIteration)}");
    Console.WriteLine($"Raw value checksum per iteration: {FormatNumber(result.RawValueChecksumPerIteration)}");
    Console.WriteLine($"Run status: {FormatProcessingArchiveQueuedOverlapStatus(result.Status)}");
    Console.WriteLine($"Consumer status: {FormatProcessingQueuedSessionStatus(result.ConsumerStatus)}");
    Console.WriteLine($"Processing completeness: {(result.ProcessingSucceeded ? "succeeded" : "failed")}");
    Console.WriteLine($"Processing succeeded batches: {FormatNumber(result.SucceededBatchCount)}");
    Console.WriteLine($"Processing failed batches: {FormatNumber(result.FailedProcessingBatchCount)}");
    Console.WriteLine($"Processing validation failed batches: {FormatNumber(result.ProcessingValidationFailedBatchCount)}");
    Console.WriteLine($"Processing canceled batches: {FormatNumber(result.CanceledBatchCount)}");
    Console.WriteLine($"Processing skipped after fault batches: {FormatNumber(result.SkippedAfterFaultBatchCount)}");
    Console.WriteLine($"Final processed batches: {FormatNumber(result.FinalProcessedBatchCount)}");
    Console.WriteLine($"Final processed stream events: {FormatNumber(result.FinalProcessedStreamEventCount)}");
    Console.WriteLine($"Final processed payload values: {FormatNumber(result.FinalProcessedPayloadValueCount)}");
    Console.WriteLine($"Final raw value checksum: {FormatNumber(result.FinalRawValueChecksum)}");
    Console.WriteLine($"Final processing checksum: {FormatUnsignedNumber(result.FinalProcessingChecksum)}");
    Console.WriteLine($"End-to-end elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
    Console.WriteLine($"Producer active ms: {FormatDecimal(result.ProducerElapsed.TotalMilliseconds)}");
    Console.WriteLine($"Consumer active ms: {FormatDecimal(result.ConsumerElapsed.TotalMilliseconds)}");
    Console.WriteLine($"Producer-consumer overlap ms: {FormatDecimal(result.OverlapElapsed.TotalMilliseconds)}");
    if (options.QueueTelemetryOutput != ProcessingBenchmarkProviderQueueTelemetryOutput.None)
    {
        PrintProcessingProviderQueueTelemetrySummary(
            result.QueueTelemetry,
            Ratio(result.QueueTelemetry.OwnedSnapshotAllocatedBytes, result.TotalPayloadValues),
            options.QueueTelemetryOutput);
    }

    PrintProcessingProviderRetentionTelemetrySummary(result.OverlapTelemetry.RetentionTelemetry);
    if (options.OverlapTelemetryOutput != ProcessingBenchmarkProviderOverlapTelemetryOutput.None)
    {
        PrintProcessingProviderOverlapTelemetrySummary(result.OverlapTelemetry, options.OverlapTelemetryOutput);
    }

    Console.WriteLine($"Compressed MB/s: {FormatDecimal(result.CompressedMegabytesPerSecond)}");
    Console.WriteLine($"Decompressed MB/s: {FormatDecimal(result.DecompressedMegabytesPerSecond)}");
    if (result.IsCache)
    {
        Console.WriteLine($"Files/s: {FormatDecimal(result.FilesPerSecond)}");
    }

    Console.WriteLine($"End-to-end stream events/s: {FormatDecimal(result.EventsPerSecond)}");
    Console.WriteLine($"End-to-end payload values/s: {FormatDecimal(result.PayloadValuesPerSecond)}");
    Console.WriteLine($"End-to-end allocated bytes: {FormatNumber(result.AllocatedBytes)}");
    Console.WriteLine("Allocation measured counter scope: global");
    Console.WriteLine($"End-to-end allocated bytes / stream event: {FormatDecimal(result.AllocatedBytesPerStreamEvent)}");
    Console.WriteLine($"End-to-end allocated bytes / payload value: {FormatDecimal(result.AllocatedBytesPerPayloadValue)}");
    if (result.WorkerTelemetry is not null)
    {
        PrintProcessingWorkerTelemetry(result.WorkerTelemetry);
    }
}

static void PrintProcessingProviderQueueTelemetryForArchiveFile(
    RadarProcessingArchiveRebalanceBenchmarkResult result,
    ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput)
{
    if (!result.HasQueueTelemetry ||
        queueTelemetryOutput == ProcessingBenchmarkProviderQueueTelemetryOutput.None)
    {
        return;
    }

    PrintProcessingProviderQueueTelemetrySummary(
        result.QueueTelemetry,
        result.OwnedSnapshotAllocatedBytesPerPayloadValue,
        queueTelemetryOutput);
}

static void PrintProcessingProviderQueueTelemetryForArchiveCache(
    RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
    ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput)
{
    if (!result.HasQueueTelemetry ||
        queueTelemetryOutput == ProcessingBenchmarkProviderQueueTelemetryOutput.None)
    {
        return;
    }

    PrintProcessingProviderQueueTelemetrySummary(
        result.QueueTelemetry,
        result.OwnedSnapshotAllocatedBytesPerPayloadValue,
        queueTelemetryOutput);
}

static void PrintProcessingProviderRetentionTelemetryForArchiveFile(
    RadarProcessingArchiveRebalanceBenchmarkResult result)
{
    if (!result.HasRetentionTelemetry)
    {
        return;
    }

    PrintProcessingProviderRetentionTelemetrySummary(result.RetentionTelemetry);
}

static void PrintProcessingProviderRetentionTelemetryForArchiveCache(
    RadarProcessingArchiveRebalanceCacheBenchmarkResult result)
{
    if (!result.HasRetentionTelemetry)
    {
        return;
    }

    PrintProcessingProviderRetentionTelemetrySummary(result.RetentionTelemetry);
}

static void PrintProcessingProviderRetentionTelemetrySummary(
    RadarProcessingRetainedPayloadTelemetrySummary telemetry)
{
    Console.WriteLine("Retained payload telemetry: summary");
    Console.WriteLine("Retained payload allocation counter scope: current-thread");
    Console.WriteLine($"Retained payload strategy: {FormatProcessingRetentionStrategy(telemetry.Strategy)}");
    Console.WriteLine($"Retained payload attempts: {FormatNumber(telemetry.RetentionAttemptCount)}");
    Console.WriteLine($"Retained payload batches: {FormatNumber(telemetry.RetainedBatchCount)}");
    Console.WriteLine($"Retained payload events: {FormatNumber(telemetry.RetainedEventCount)}");
    Console.WriteLine($"Retained payload bytes: {FormatNumber(telemetry.RetainedPayloadBytes)}");
    Console.WriteLine($"Retained payload values: {FormatNumber(telemetry.RetainedPayloadValueCount)}");
    Console.WriteLine($"Retained payload allocated bytes: {FormatNumber(telemetry.AllocatedBytes)}");
    Console.WriteLine($"Retained payload elapsed ms: {FormatDecimal(telemetry.TotalRetentionTime.TotalMilliseconds)}");
    Console.WriteLine($"Retained payload transfers: {FormatNumber(telemetry.TransferCount)}");
    Console.WriteLine($"Retained payload pool rents: {FormatNumber(telemetry.PoolRentCount)}");
    Console.WriteLine($"Retained payload pool returns: {FormatNumber(telemetry.PoolReturnCount)}");
    Console.WriteLine($"Retained payload pool misses: {FormatNumber(telemetry.PoolMissCount)}");
    Console.WriteLine($"Retained event array pool rents: {FormatNumber(telemetry.EventPoolRentCount)}");
    Console.WriteLine($"Retained event array pool returns: {FormatNumber(telemetry.EventPoolReturnCount)}");
    Console.WriteLine($"Retained event array pool misses: {FormatNumber(telemetry.EventPoolMissCount)}");
    Console.WriteLine($"Retained byte array pool rents: {FormatNumber(telemetry.PayloadPoolRentCount)}");
    Console.WriteLine($"Retained byte array pool returns: {FormatNumber(telemetry.PayloadPoolReturnCount)}");
    Console.WriteLine($"Retained byte array pool misses: {FormatNumber(telemetry.PayloadPoolMissCount)}");
    Console.WriteLine($"Retained payload unsupported strategy attempts: {FormatNumber(telemetry.RetentionUnsupportedStrategyCount)}");
    Console.WriteLine($"Retained payload failed copies: {FormatNumber(telemetry.RetentionFailedCopyCount)}");
    Console.WriteLine($"Retained payload canceled retentions: {FormatNumber(telemetry.RetentionCanceledCount)}");
    Console.WriteLine($"Retained payload invalid inputs: {FormatNumber(telemetry.RetentionInvalidInputCount)}");
    Console.WriteLine($"Retained payload release attempts: {FormatNumber(telemetry.ReleaseAttemptCount)}");
    Console.WriteLine($"Retained payload released batches: {FormatNumber(telemetry.ReleasedBatchCount)}");
    Console.WriteLine($"Retained payload already released batches: {FormatNumber(telemetry.AlreadyReleasedBatchCount)}");
    Console.WriteLine($"Retained payload release-not-required batches: {FormatNumber(telemetry.ReleaseNotRequiredCount)}");
    Console.WriteLine($"Retained payload failed releases: {FormatNumber(telemetry.ReleaseFailedCount)}");
    Console.WriteLine($"Retained payload release elapsed ms: {FormatDecimal(telemetry.TotalReleaseTime.TotalMilliseconds)}");
}

static void PrintProcessingProviderOverlapTelemetryForArchiveFile(
    RadarProcessingArchiveRebalanceBenchmarkResult result,
    ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput)
{
    if (!result.HasOverlapTelemetry ||
        overlapTelemetryOutput == ProcessingBenchmarkProviderOverlapTelemetryOutput.None)
    {
        return;
    }

    PrintProcessingProviderOverlapTelemetrySummary(result.OverlapTelemetry, overlapTelemetryOutput);
}

static void PrintProcessingProviderOverlapTelemetryForArchiveCache(
    RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
    ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput)
{
    if (!result.HasOverlapTelemetry ||
        overlapTelemetryOutput == ProcessingBenchmarkProviderOverlapTelemetryOutput.None)
    {
        return;
    }

    PrintProcessingProviderOverlapTelemetrySummary(result.OverlapTelemetry, overlapTelemetryOutput);
}

static void PrintProcessingProviderOverlapTelemetrySummary(
    RadarProcessingArchiveOverlapTelemetrySummary telemetry,
    ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput)
{
    Console.WriteLine($"Provider overlap telemetry: {FormatProviderOverlapTelemetryOutput(overlapTelemetryOutput)}");
    Console.WriteLine($"Provider overlap retained payload strategy: {FormatProcessingRetentionStrategy(telemetry.RetentionStrategy)}");
    Console.WriteLine($"Provider overlap elapsed ms: {FormatDecimal(telemetry.Elapsed.TotalMilliseconds)}");
    Console.WriteLine($"Provider overlap producer active ms: {FormatDecimal(telemetry.ProducerActiveTime.TotalMilliseconds)}");
    Console.WriteLine($"Provider overlap consumer active ms: {FormatDecimal(telemetry.ConsumerActiveTime.TotalMilliseconds)}");
    Console.WriteLine($"Provider overlap shared active ms: {FormatDecimal(telemetry.OverlapElapsed.TotalMilliseconds)}");
    Console.WriteLine($"Provider overlap has producer-consumer overlap: {FormatBoolean(telemetry.HasProducerConsumerOverlap)}");
    Console.WriteLine($"Provider overlap has queued-ahead overlap: {FormatBoolean(telemetry.HasQueuedAheadOverlap)}");
    Console.WriteLine($"Provider overlap queue depth high watermark: {FormatNumber(telemetry.QueueDepthHighWatermark)}");
    Console.WriteLine($"Provider overlap retained payload bytes high watermark: {FormatNumber(telemetry.RetainedPayloadBytesHighWatermark)}");
    PrintProcessingRetainedResourcePressureSummary("Provider overlap", telemetry.RetainedResourcePressure);
    Console.WriteLine($"Provider overlap provider blocked ms: {FormatDecimal(telemetry.ProviderBlockedTime.TotalMilliseconds)}");
    Console.WriteLine($"Provider overlap consumer idle ms: {FormatDecimal(telemetry.ConsumerIdleTime.TotalMilliseconds)}");
    Console.WriteLine($"Provider overlap provider-to-processing latency ms: {FormatDecimal(telemetry.TotalProviderToProcessingLatency.TotalMilliseconds)}");
    Console.WriteLine($"Provider overlap retained batches: {FormatNumber(telemetry.RetainedBatchCount)}");
    Console.WriteLine($"Provider overlap retained events: {FormatNumber(telemetry.RetainedEventCount)}");
    Console.WriteLine($"Provider overlap retained payload bytes: {FormatNumber(telemetry.RetainedPayloadBytes)}");
    Console.WriteLine($"Provider overlap retained payload values: {FormatNumber(telemetry.RetainedPayloadValueCount)}");
    Console.WriteLine($"Provider overlap retention allocated bytes: {FormatNumber(telemetry.RetentionAllocatedBytes)}");
    Console.WriteLine("Provider overlap measured allocation counter scope: global");
    Console.WriteLine($"Provider overlap measured allocated bytes: {FormatNumber(telemetry.MeasuredAllocatedBytes)}");
    Console.WriteLine($"Provider overlap unattributed allocated bytes: {FormatNumber(telemetry.UnattributedAllocatedBytes)}");
    Console.WriteLine($"Provider overlap release attempts: {FormatNumber(telemetry.ReleaseAttemptCount)}");
    Console.WriteLine($"Provider overlap released batches: {FormatNumber(telemetry.ReleasedBatchCount)}");
    Console.WriteLine($"Provider overlap release-not-required batches: {FormatNumber(telemetry.ReleaseNotRequiredCount)}");
    Console.WriteLine($"Provider overlap failed releases: {FormatNumber(telemetry.ReleaseFailedCount)}");
}

static bool IsDefaultCandidateFileBenchmarkContour(
    RadarProcessingArchiveRebalanceBenchmarkResult result,
    ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
    ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
    ProcessingBenchmarkArchiveRebalanceOptions.MatchesDefaultCandidateContour(
        result.ProviderMode,
        result.QueueCapacity,
        result.ProviderOverlapMode,
        result.RetentionStrategy,
        result.QueueRetainedPayloadBytes,
        result.OverlapConsumerDelay,
        queueTelemetryOutput,
        overlapTelemetryOutput,
        result.ExecutionMode);

static bool IsDefaultCandidateCacheBenchmarkContour(
    RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
    ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
    ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
    ProcessingBenchmarkArchiveRebalanceOptions.MatchesDefaultCandidateContour(
        result.ProviderMode,
        result.QueueCapacity,
        result.ProviderOverlapMode,
        result.RetentionStrategy,
        result.QueueRetainedPayloadBytes,
        result.OverlapConsumerDelay,
        queueTelemetryOutput,
        overlapTelemetryOutput,
        result.ExecutionMode);

static string FormatProviderOverlapEvidenceContourForFileBenchmark(
    RadarProcessingArchiveRebalanceBenchmarkResult result,
    ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
    ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
    FormatProviderOverlapEvidenceContourCore(
        result.ProviderMode,
        result.ProviderOverlapMode,
        result.OverlapConsumerDelay,
        IsDefaultCandidateFileBenchmarkContour(result, queueTelemetryOutput, overlapTelemetryOutput));

static string FormatProviderOverlapEvidenceContourForCacheBenchmark(
    RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
    ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
    ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
    FormatProviderOverlapEvidenceContourCore(
        result.ProviderMode,
        result.ProviderOverlapMode,
        result.OverlapConsumerDelay,
        IsDefaultCandidateCacheBenchmarkContour(result, queueTelemetryOutput, overlapTelemetryOutput));

static string FormatProviderOverlapEvidenceContourCore(
    RadarProcessingArchiveProviderMode providerMode,
    RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
    TimeSpan overlapConsumerDelay,
    bool isDefaultCandidateContour) =>
    ProcessingBenchmarkArchiveRebalanceOptions.FormatProviderOverlapEvidenceContour(
        providerMode,
        providerOverlapMode,
        overlapConsumerDelay,
        isDefaultCandidateContour);

static string FormatProviderOverlapEvidenceScope(string providerOverlapEvidenceContour) =>
    ProcessingBenchmarkArchiveRebalanceOptions.FormatProviderOverlapEvidenceScope(providerOverlapEvidenceContour);

static void PrintProcessingProviderQueueTelemetrySummary(
    RadarProcessingProviderQueueTelemetrySummary telemetry,
    double ownedSnapshotAllocatedBytesPerPayloadValue,
    ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput)
{
    Console.WriteLine($"Provider queue telemetry: {FormatProviderQueueTelemetryOutput(queueTelemetryOutput)}");
    Console.WriteLine($"Provider queue owned snapshots: {FormatNumber(telemetry.OwnedSnapshotCount)}");
    Console.WriteLine($"Provider queue owned snapshot events: {FormatNumber(telemetry.OwnedSnapshotEventCount)}");
    Console.WriteLine($"Provider queue owned snapshot payload bytes: {FormatNumber(telemetry.OwnedSnapshotPayloadBytes)}");
    Console.WriteLine($"Provider queue owned snapshot payload values: {FormatNumber(telemetry.OwnedSnapshotPayloadValueCount)}");
    Console.WriteLine($"Provider queue owned snapshot elapsed ms: {FormatDecimal(telemetry.TotalOwnedSnapshotTime.TotalMilliseconds)}");
    Console.WriteLine($"Provider queue owned snapshot allocated bytes: {FormatNumber(telemetry.OwnedSnapshotAllocatedBytes)}");
    Console.WriteLine($"Provider queue owned snapshot allocated bytes / payload value: {FormatDecimal(ownedSnapshotAllocatedBytesPerPayloadValue)}");
    Console.WriteLine($"Provider queue enqueue attempts: {FormatNumber(telemetry.EnqueueAttemptCount)}");
    Console.WriteLine($"Provider queue enqueued batches: {FormatNumber(telemetry.EnqueuedBatchCount)}");
    Console.WriteLine($"Provider queue full batches: {FormatNumber(telemetry.EnqueueFullCount)}");
    Console.WriteLine($"Provider queue timed out batches: {FormatNumber(telemetry.EnqueueTimedOutCount)}");
    Console.WriteLine($"Provider queue canceled enqueue batches: {FormatNumber(telemetry.EnqueueCanceledCount)}");
    Console.WriteLine($"Provider queue closed enqueue batches: {FormatNumber(telemetry.EnqueueClosedCount)}");
    Console.WriteLine($"Provider queue faulted enqueue batches: {FormatNumber(telemetry.EnqueueFaultedCount)}");
    Console.WriteLine($"Provider queue enqueue wait ms: {FormatDecimal(telemetry.TotalEnqueueWaitTime.TotalMilliseconds)}");
    Console.WriteLine($"Provider queue dequeued batches: {FormatNumber(telemetry.DequeuedBatchCount)}");
    Console.WriteLine($"Provider queue completed batches: {FormatNumber(telemetry.CompletedBatchCount)}");
    Console.WriteLine($"Provider queue failed batches: {FormatNumber(telemetry.FailedBatchCount)}");
    Console.WriteLine($"Provider queue canceled batches: {FormatNumber(telemetry.CanceledBatchCount)}");
    Console.WriteLine($"Provider queue skipped after fault batches: {FormatNumber(telemetry.SkippedAfterFaultCount)}");
    Console.WriteLine($"Provider queue drain ms: {FormatDecimal(telemetry.TotalDrainTime.TotalMilliseconds)}");
    Console.WriteLine($"Provider queue dequeue wait ms: {FormatDecimal(telemetry.TotalDequeueWaitTime.TotalMilliseconds)}");
    Console.WriteLine($"Provider queue depth high watermark: {FormatNumber(telemetry.QueueDepthHighWatermark)}");
    Console.WriteLine($"Provider queue payload bytes high watermark: {FormatNumber(telemetry.QueuedPayloadBytesHighWatermark)}");
    Console.WriteLine($"Provider queue retained payload bytes high watermark: {FormatNumber(telemetry.RetainedPayloadBytesHighWatermark)}");
    PrintProcessingRetainedResourcePressureSummary("Provider queue", telemetry.RetainedResourcePressure);
    Console.WriteLine($"Provider-to-processing latency ms: {FormatDecimal(telemetry.TotalProviderToProcessingLatency.TotalMilliseconds)}");
    Console.WriteLine($"Provider queue retained recent details: {FormatNumber(telemetry.RetainedRecentDetailCount)}");
    Console.WriteLine($"Provider queue dropped recent details: {FormatNumber(telemetry.DroppedRecentDetailCount)}");

    if (queueTelemetryOutput == ProcessingBenchmarkProviderQueueTelemetryOutput.Recent)
    {
        PrintProcessingProviderQueueRecentDetails(telemetry.RecentDetails);
    }
}

static void PrintProcessingProviderQueueRecentDetails(
    IReadOnlyList<RadarProcessingProviderQueueRecentDetail> recentDetails)
{
    if (recentDetails.Count == 0)
    {
        Console.WriteLine("Provider queue recent details: (none)");
        return;
    }

    Console.WriteLine("Provider queue recent details:");
    for (var i = 0; i < recentDetails.Count; i++)
    {
        var detail = recentDetails[i];
        Console.WriteLine(
            $"  {FormatNumber(i + 1)}. {FormatProcessingProviderQueueRecentDetailKind(detail.Kind)} " +
            $"sequence {FormatProcessingProviderQueueSequence(detail.Sequence)} " +
            $"enqueue {FormatProcessingProviderQueueEnqueueStatus(detail.EnqueueStatus)} " +
            $"processing {FormatProcessingProviderQueueProcessingStatus(detail.ProcessingStatus)} " +
            $"events {FormatNumber(detail.StreamEventCount)} payload bytes {FormatNumber(detail.PayloadBytes)} " +
            $"payload values {FormatNumber(detail.PayloadValueCount)} queue depth {FormatNumber(detail.QueueDepth)}");
    }
}

static void PrintProcessingRetainedResourcePressureSummary(
    string prefix,
    RadarProcessingRetainedResourcePressureSummary telemetry)
{
    Console.WriteLine($"{prefix} current pending retained batches: {FormatNumber(telemetry.CurrentPendingRetainedBatchCount)}");
    Console.WriteLine($"{prefix} current pending retained payload bytes: {FormatNumber(telemetry.CurrentPendingRetainedPayloadBytes)}");
    Console.WriteLine($"{prefix} pending retained batches high watermark: {FormatNumber(telemetry.PendingRetainedBatchCountHighWatermark)}");
    Console.WriteLine($"{prefix} pending retained payload bytes high watermark: {FormatNumber(telemetry.PendingRetainedPayloadBytesHighWatermark)}");
    Console.WriteLine($"{prefix} current active retained batches: {FormatNumber(telemetry.CurrentActiveRetainedBatchCount)}");
    Console.WriteLine($"{prefix} current active retained payload bytes: {FormatNumber(telemetry.CurrentActiveRetainedPayloadBytes)}");
    Console.WriteLine($"{prefix} active retained batches high watermark: {FormatNumber(telemetry.ActiveRetainedBatchCountHighWatermark)}");
    Console.WriteLine($"{prefix} active retained payload bytes high watermark: {FormatNumber(telemetry.ActiveRetainedPayloadBytesHighWatermark)}");
    Console.WriteLine($"{prefix} current combined retained batches: {FormatNumber(telemetry.CurrentCombinedRetainedBatchCount)}");
    Console.WriteLine($"{prefix} current combined retained payload bytes: {FormatNumber(telemetry.CurrentCombinedRetainedPayloadBytes)}");
    Console.WriteLine($"{prefix} combined retained batches high watermark: {FormatNumber(telemetry.CombinedRetainedBatchCountHighWatermark)}");
    Console.WriteLine($"{prefix} combined retained payload bytes high watermark: {FormatNumber(telemetry.CombinedRetainedPayloadBytesHighWatermark)}");
}

static void PrintProcessingRebalanceMovePressures(
    IReadOnlyList<RadarProcessingSyntheticRebalanceMovePressure> acceptedMovePressures)
{
    const int displayedMovePressureLimit = 8;

    if (acceptedMovePressures.Count == 0)
    {
        Console.WriteLine("Accepted move pressures: (none)");
        return;
    }

    Console.WriteLine("Accepted move pressures:");
    var displayedCount = Math.Min(acceptedMovePressures.Count, displayedMovePressureLimit);
    for (var i = 0; i < displayedCount; i++)
    {
        var pressure = acceptedMovePressures[i];
        Console.WriteLine(
            $"  {FormatNumber(i + 1)}. {FormatProcessingRebalanceMoveKind(pressure.MoveKind)} " +
            $"source {FormatDecimal(pressure.SourceShardBefore)}->{FormatDecimal(pressure.SourceShardAfter)}, " +
            $"target {FormatDecimal(pressure.TargetShardBefore)}->{FormatDecimal(pressure.TargetShardAfter)}, " +
            $"relief {FormatDecimal(pressure.ExpectedRelief)}");
    }

    var omittedCount = acceptedMovePressures.Count - displayedCount;
    if (omittedCount > 0)
    {
        Console.WriteLine($"  ... {FormatNumber(omittedCount)} more accepted move pressure samples omitted");
    }
}

static void PrintProcessingRebalanceRetentionStats(
    RadarProcessingRebalanceRetentionStats stats)
{
    Console.WriteLine($"Retained decisions: {FormatNumber(stats.RetainedDecisionCount)}");
    Console.WriteLine($"Dropped decision details: {FormatNumber(stats.DroppedDecisionCount)}");
    Console.WriteLine($"Retained lifecycle transitions: {FormatNumber(stats.RetainedLifecycleTransitionCount)}");
    Console.WriteLine($"Dropped lifecycle transition details: {FormatNumber(stats.DroppedLifecycleTransitionCount)}");
    Console.WriteLine($"Retained accepted moves: {FormatNumber(stats.RetainedAcceptedMoveCount)}");
    Console.WriteLine($"Dropped accepted move details: {FormatNumber(stats.DroppedAcceptedMoveCount)}");
    Console.WriteLine($"Retained validation failures: {FormatNumber(stats.RetainedValidationFailureCount)}");
    Console.WriteLine($"Dropped validation failure details: {FormatNumber(stats.DroppedValidationFailureCount)}");
}

static void PrintProcessingQuarantineLifecycle(
    int quarantineTtlEvaluations,
    int sustainedCoolingSampleCount,
    double materialPressureChangeThreshold)
{
    Console.WriteLine($"Quarantine TTL evaluations: {FormatNumber(quarantineTtlEvaluations)}");
    Console.WriteLine($"Quarantine sustained cooling samples: {FormatNumber(sustainedCoolingSampleCount)}");
    Console.WriteLine($"Quarantine material pressure change: {FormatDecimal(materialPressureChangeThreshold)}");
}

static void PrintProcessingPressureSkew(
    RadarProcessingPressureSkewOptions options)
{
    Console.WriteLine($"Synthetic pressure overlay: {FormatBoolean(options.IsEnabled)}");
    Console.WriteLine($"Pressure skew profile: {FormatProcessingPressureSkewProfile(options.Profile)}");
    Console.WriteLine($"Pressure skew factor: {FormatDecimal(options.Factor)}");
    Console.WriteLine($"Pressure skew period: {FormatNumber(options.Period)}");
}

static string FormatProcessingMode(RadarProcessingExecutionMode executionMode) =>
    executionMode switch
    {
        RadarProcessingExecutionMode.Sequential => "sequential",
        RadarProcessingExecutionMode.PartitionedBarrier => "partitioned",
        RadarProcessingExecutionMode.AsyncShardTransport => "async",
        _ => executionMode.ToString()
    };

static string FormatProcessingHandlerSet(RadarProcessingBenchmarkHandlerSet handlerSet) =>
    handlerSet switch
    {
        RadarProcessingBenchmarkHandlerSet.None => "none",
        RadarProcessingBenchmarkHandlerSet.CounterChecksum => "counter-checksum",
        _ => handlerSet.ToString()
    };

static string FormatProcessingRebalanceWorkload(RadarProcessingSyntheticRebalanceWorkloadKind workloadKind) =>
    workloadKind switch
    {
        RadarProcessingSyntheticRebalanceWorkloadKind.Balanced => "balanced",
        RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard => "hot-shard",
        RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition => "intrinsic-hot",
        RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike => "oscillating",
        RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm => "cooldown-storm",
        RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry => "quarantine-ttl-retry",
        RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear =>
            "quarantine-cooling-clear",
        RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry =>
            "quarantine-pressure-change-retry",
        RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry =>
            "quarantine-retry-reentry",
        RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear =>
            "quarantine-successful-relief-clear",
        RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard => "long-no-hot-shard",
        RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection => "long-cooldown-rejection",
        RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection =>
            "long-unsafe-target-rejection",
        RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons =>
            "long-mixed-skipped-reasons",
        RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention => "counters-only-retention",
        _ => workloadKind.ToString()
    };

static string FormatProcessingRebalanceMode(RadarProcessingSyntheticRebalanceBenchmarkMode mode) =>
    mode switch
    {
        RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance => "static",
        RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly => "sampling",
        RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession => "rebalance-session",
        _ => mode.ToString()
    };

static string FormatProcessingValidationProfile(RadarProcessingValidationProfile profile) =>
    profile switch
    {
        RadarProcessingValidationProfile.Off => "off",
        RadarProcessingValidationProfile.Essential => "essential",
        RadarProcessingValidationProfile.Diagnostic => "diagnostic",
        RadarProcessingValidationProfile.Benchmark => "benchmark",
        _ => profile.ToString()
    };

static string FormatProcessingRetentionMode(RadarProcessingDiagnosticRetentionMode retentionMode) =>
    retentionMode switch
    {
        RadarProcessingDiagnosticRetentionMode.Counters => "counters",
        RadarProcessingDiagnosticRetentionMode.Recent => "recent",
        RadarProcessingDiagnosticRetentionMode.Diagnostic => "diagnostic",
        _ => retentionMode.ToString()
    };

static string FormatProcessingPressureSkewProfile(RadarProcessingPressureSkewProfile profile) =>
    profile switch
    {
        RadarProcessingPressureSkewProfile.None => "none",
        RadarProcessingPressureSkewProfile.HotShard => "hot-shard",
        RadarProcessingPressureSkewProfile.RotatingHotShard => "rotating-hot-shard",
        RadarProcessingPressureSkewProfile.HotPartition => "hot-partition",
        RadarProcessingPressureSkewProfile.TargetStarvation => "target-starvation",
        RadarProcessingPressureSkewProfile.BudgetStorm => "budget-storm",
        _ => profile.ToString()
    };

static string FormatProcessingArchiveProviderMode(RadarProcessingArchiveProviderMode providerMode) =>
    providerMode switch
    {
        RadarProcessingArchiveProviderMode.BlockingBorrowed => "blocking-borrowed",
        RadarProcessingArchiveProviderMode.QueuedOwned => "queued-owned",
        _ => providerMode.ToString()
    };

static string FormatProcessingProviderOverlapMode(RadarProcessingQueuedProviderOverlapMode providerOverlapMode) =>
    providerOverlapMode switch
    {
        RadarProcessingQueuedProviderOverlapMode.None => "none",
        RadarProcessingQueuedProviderOverlapMode.ProducerConsumer => "producer-consumer",
        _ => providerOverlapMode.ToString()
    };

static string FormatProcessingRetentionStrategy(RadarProcessingRetainedPayloadStrategy retentionStrategy) =>
    retentionStrategy switch
    {
        RadarProcessingRetainedPayloadStrategy.SnapshotCopy => "snapshot-copy",
        RadarProcessingRetainedPayloadStrategy.PooledCopy => "pooled-copy",
        RadarProcessingRetainedPayloadStrategy.BuilderTransfer => "builder-transfer",
        _ => retentionStrategy.ToString()
    };

static string FormatProcessingBenchmarkApplicableOptionValueSource(
    ProcessingBenchmarkOptionValueSource source,
    bool isApplicable) =>
    isApplicable
        ? FormatProcessingBenchmarkOptionValueSource(source)
        : "not-applicable";

static string FormatProcessingBenchmarkOptionValueSource(ProcessingBenchmarkOptionValueSource source) =>
    source switch
    {
        ProcessingBenchmarkOptionValueSource.CurrentDefault => "current-default",
        ProcessingBenchmarkOptionValueSource.Explicit => "explicit",
        ProcessingBenchmarkOptionValueSource.RolloutDefault => "rollout-default",
        _ => source.ToString()
    };

static string FormatProviderQueueTelemetryOutput(ProcessingBenchmarkProviderQueueTelemetryOutput output) =>
    output switch
    {
        ProcessingBenchmarkProviderQueueTelemetryOutput.None => "none",
        ProcessingBenchmarkProviderQueueTelemetryOutput.Summary => "summary",
        ProcessingBenchmarkProviderQueueTelemetryOutput.Recent => "recent",
        _ => output.ToString()
    };

static string FormatProviderOverlapTelemetryOutput(ProcessingBenchmarkProviderOverlapTelemetryOutput output) =>
    output switch
    {
        ProcessingBenchmarkProviderOverlapTelemetryOutput.None => "none",
        ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary => "summary",
        ProcessingBenchmarkProviderOverlapTelemetryOutput.Recent => "recent",
        _ => output.ToString()
    };

static string FormatProcessingProviderQueueRecentDetailKind(RadarProcessingProviderQueueRecentDetailKind kind) =>
    kind switch
    {
        RadarProcessingProviderQueueRecentDetailKind.Enqueue => "enqueue",
        RadarProcessingProviderQueueRecentDetailKind.Dequeue => "dequeue",
        RadarProcessingProviderQueueRecentDetailKind.Processing => "processing",
        _ => kind.ToString()
    };

static string FormatProcessingProviderQueueSequence(RadarProcessingQueuedBatchSequence? sequence) =>
    sequence.HasValue
        ? FormatNumber(sequence.Value.Value)
        : "n/a";

static string FormatProcessingProviderQueueEnqueueStatus(RadarProcessingQueuedBatchEnqueueStatus? status) =>
    status switch
    {
        RadarProcessingQueuedBatchEnqueueStatus.Accepted => "accepted",
        RadarProcessingQueuedBatchEnqueueStatus.Full => "full",
        RadarProcessingQueuedBatchEnqueueStatus.TimedOut => "timed-out",
        RadarProcessingQueuedBatchEnqueueStatus.Canceled => "canceled",
        RadarProcessingQueuedBatchEnqueueStatus.Closed => "closed",
        RadarProcessingQueuedBatchEnqueueStatus.Faulted => "faulted",
        null => "n/a",
        _ => status.Value.ToString()
    };

static string FormatProcessingProviderQueueProcessingStatus(RadarProcessingQueuedBatchProcessingStatus? status) =>
    status switch
    {
        RadarProcessingQueuedBatchProcessingStatus.Succeeded => "succeeded",
        RadarProcessingQueuedBatchProcessingStatus.FailedProcessing => "failed-processing",
        RadarProcessingQueuedBatchProcessingStatus.FailedValidation => "failed-validation",
        RadarProcessingQueuedBatchProcessingStatus.FailedMigration => "failed-migration",
        RadarProcessingQueuedBatchProcessingStatus.Canceled => "canceled",
        RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault => "skipped-after-fault",
        null => "n/a",
        _ => status.Value.ToString()
    };

static string FormatProcessingArchiveQueuedOverlapStatus(RadarProcessingArchiveQueuedOverlapStatus status) =>
    status switch
    {
        RadarProcessingArchiveQueuedOverlapStatus.NotStarted => "not-started",
        RadarProcessingArchiveQueuedOverlapStatus.Completed => "completed",
        RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed => "producer-failed",
        RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted => "consumer-faulted",
        RadarProcessingArchiveQueuedOverlapStatus.Canceled => "canceled",
        RadarProcessingArchiveQueuedOverlapStatus.Disposed => "disposed",
        _ => status.ToString()
    };

static string FormatProcessingQueuedSessionStatus(RadarProcessingQueuedSessionStatus status) =>
    status switch
    {
        RadarProcessingQueuedSessionStatus.NotStarted => "not-started",
        RadarProcessingQueuedSessionStatus.Running => "running",
        RadarProcessingQueuedSessionStatus.Draining => "draining",
        RadarProcessingQueuedSessionStatus.Completed => "completed",
        RadarProcessingQueuedSessionStatus.Faulted => "faulted",
        RadarProcessingQueuedSessionStatus.Canceled => "canceled",
        RadarProcessingQueuedSessionStatus.Disposed => "disposed",
        _ => status.ToString()
    };

static string FormatBoolean(bool value) =>
    value ? "yes" : "no";

static string FormatProcessingRebalanceMoveKind(RadarProcessingRebalanceMoveKind moveKind) =>
    moveKind switch
    {
        RadarProcessingRebalanceMoveKind.DirectHotRelief => "direct-hot-relief",
        RadarProcessingRebalanceMoveKind.ColdEvacuation => "cold-evacuation",
        RadarProcessingRebalanceMoveKind.RoomMakingReserved => "room-making-reserved",
        _ => moveKind.ToString()
    };

static string FormatProcessingRebalanceSkippedReasons(
    IReadOnlyList<RadarProcessingRebalanceSkippedReason> skippedReasons) =>
    skippedReasons.Count == 0
        ? "(none)"
        : string.Join(", ", skippedReasons.Select(FormatProcessingRebalanceSkippedReason));

static string FormatProcessingRebalanceSkippedReasonCounters(
    IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> counters) =>
    counters.Count == 0
        ? "(none)"
        : string.Join(", ", counters.Select(counter =>
            $"{FormatProcessingRebalanceSkippedReason(counter.Reason)}={FormatNumber(counter.Count)}"));

static string FormatProcessingRebalanceSkippedReason(RadarProcessingRebalanceSkippedReason reason) =>
    reason switch
    {
        RadarProcessingRebalanceSkippedReason.NoSustainedPressure => "no-sustained-pressure",
        RadarProcessingRebalanceSkippedReason.NoHotShard => "no-hot-shard",
        RadarProcessingRebalanceSkippedReason.NoColdTargetShard => "no-cold-target-shard",
        RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget =>
            "direct-hot-partition-has-no-safe-target",
        RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit => "insufficient-projected-benefit",
        RadarProcessingRebalanceSkippedReason.TargetWouldBecomeWarm => "target-would-become-warm",
        RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot => "target-would-become-hot",
        RadarProcessingRebalanceSkippedReason.TargetHeadroomExceeded => "target-headroom-exceeded",
        RadarProcessingRebalanceSkippedReason.CandidatePartitionInCooldown => "candidate-partition-in-cooldown",
        RadarProcessingRebalanceSkippedReason.CandidatePartitionBelowMinimumResidency =>
            "candidate-partition-below-minimum-residency",
        RadarProcessingRebalanceSkippedReason.SourceShardInCooldown => "source-shard-in-cooldown",
        RadarProcessingRebalanceSkippedReason.TargetShardInCooldown => "target-shard-in-cooldown",
        RadarProcessingRebalanceSkippedReason.SourceShardMoveBudgetExhausted =>
            "source-shard-move-budget-exhausted",
        RadarProcessingRebalanceSkippedReason.TargetShardReceiveBudgetExhausted =>
            "target-shard-receive-budget-exhausted",
        RadarProcessingRebalanceSkippedReason.GlobalMoveBudgetExhausted => "global-move-budget-exhausted",
        RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot =>
            "partition-classified-intrinsic-hot",
        RadarProcessingRebalanceSkippedReason.PartitionQuarantined => "partition-quarantined",
        RadarProcessingRebalanceSkippedReason.ColdEvacuationInsufficientBenefit =>
            "cold-evacuation-insufficient-benefit",
        RadarProcessingRebalanceSkippedReason.MigrationValidationFailed => "migration-validation-failed",
        _ => reason.ToString()
    };

static int BenchmarkArchiveDecompression(string[] args)
{
    var options = ArchiveBenchmarkDecompressionOptions.Parse(args);
    var result = new NexradArchiveDecompressionBenchmark().Measure(
        options.FilePath,
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
    Console.WriteLine($"File size bytes: {FormatNumber(result.FileSizeBytes)}");
    Console.WriteLine($"Compressed records per iteration: {FormatNumber(result.CompressedRecordsPerIteration)}");
    Console.WriteLine($"Compressed bytes per iteration: {FormatNumber(result.CompressedBytesPerIteration)}");
    Console.WriteLine($"Decompressed bytes per iteration: {FormatNumber(result.DecompressedBytesPerIteration)}");
    Console.WriteLine($"Total compressed records: {FormatNumber(result.TotalCompressedRecords)}");
    Console.WriteLine($"Total compressed bytes: {FormatNumber(result.TotalCompressedBytes)}");
    Console.WriteLine($"Total decompressed bytes: {FormatNumber(result.TotalDecompressedBytes)}");
    Console.WriteLine($"Elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
    Console.WriteLine($"Compressed MB/s: {FormatDecimal(MegabytesPerSecond(result.TotalCompressedBytes, result.Elapsed))}");
    Console.WriteLine($"Decompressed MB/s: {FormatDecimal(MegabytesPerSecond(result.TotalDecompressedBytes, result.Elapsed))}");
    Console.WriteLine($"Records/s: {FormatDecimal(PerSecond(result.TotalCompressedRecords, result.Elapsed))}");
    Console.WriteLine($"Allocated bytes: {FormatNumber(result.AllocatedBytes)}");
    Console.WriteLine($"Allocated bytes / decompressed MB: {FormatDecimal(result.AllocatedBytes / Math.Max(result.TotalDecompressedBytes / 1_000_000d, 1d))}");
    Console.WriteLine($"Allocated bytes / record: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalCompressedRecords, 1d))}");
    return 0;
}

static int BenchmarkArchiveParse(string[] args)
{
    var options = ArchiveBenchmarkParseOptions.Parse(args);
    var result = new NexradArchiveParseBenchmark().Measure(
        options.FilePath,
        options.Iterations,
        options.WarmupIterations,
        options.Parallelism,
        options.Decompressor,
        options.DecodeMomentValues,
        options.DecodeCalibratedMomentValues,
        CancellationToken.None);

    Console.WriteLine($"File: {result.FilePath}");
    Console.WriteLine($"Decompressor: {result.Decompressor}");
    Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
    Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
    Console.WriteLine($"Parallelism: {FormatNumber(result.DegreeOfParallelism)}");
    Console.WriteLine($"Decode moment values: {result.DecodeMomentValues}");
    Console.WriteLine($"Decode calibrated moment values: {result.DecodeCalibratedMomentValues}");
    Console.WriteLine($"File size bytes: {FormatNumber(result.FileSizeBytes)}");
    Console.WriteLine($"Compressed records per iteration: {FormatNumber(result.CompressedRecordsPerIteration)}");
    Console.WriteLine($"Compressed bytes per iteration: {FormatNumber(result.CompressedBytesPerIteration)}");
    Console.WriteLine($"Decompressed bytes per iteration: {FormatNumber(result.DecompressedBytesPerIteration)}");
    Console.WriteLine($"Messages per iteration: {FormatNumber(result.MessagesPerIteration)}");
    Console.WriteLine($"Type 31 radials per iteration: {FormatNumber(result.Type31RadialsPerIteration)}");
    Console.WriteLine($"Estimated gate-moment events per iteration: {FormatNumber(result.EstimatedGateMomentEventsPerIteration)}");
    if (result.DecodeMomentValues)
    {
        Console.WriteLine($"Decoded gate-moment values per iteration: {FormatNumber(result.DecodedGateMomentValuesPerIteration)}");
        Console.WriteLine($"Decoded gate-moment value checksum per iteration: {FormatUnsignedNumber(result.DecodedGateMomentValueChecksumPerIteration)}");
    }

    if (result.DecodeCalibratedMomentValues)
    {
        Console.WriteLine($"Calibrated gate-moment values per iteration: {FormatNumber(result.CalibratedGateMomentValuesPerIteration)}");
        Console.WriteLine($"Below-threshold gate-moment values per iteration: {FormatNumber(result.BelowThresholdGateMomentValuesPerIteration)}");
        Console.WriteLine($"Range-folded gate-moment values per iteration: {FormatNumber(result.RangeFoldedGateMomentValuesPerIteration)}");
        Console.WriteLine($"CFP filter-not-applied values per iteration: {FormatNumber(result.ClutterFilterNotAppliedGateMomentValuesPerIteration)}");
        Console.WriteLine($"CFP point-clutter-filter values per iteration: {FormatNumber(result.PointClutterFilterAppliedGateMomentValuesPerIteration)}");
        Console.WriteLine($"CFP dual-pol-filtered values per iteration: {FormatNumber(result.DualPolarizationFilteredGateMomentValuesPerIteration)}");
        Console.WriteLine($"Reserved gate-moment values per iteration: {FormatNumber(result.ReservedGateMomentValuesPerIteration)}");
        Console.WriteLine($"Unsupported calibrated gate-moment values per iteration: {FormatNumber(result.UnsupportedCalibratedGateMomentValuesPerIteration)}");
        Console.WriteLine($"Calibrated gate-moment value scaled checksum per iteration: {FormatNumber(result.CalibratedGateMomentValueScaledChecksumPerIteration)}");
        Console.WriteLine($"Calibrated value range per iteration: {FormatCompactDouble(result.MinimumCalibratedGateMomentValuePerIteration)}..{FormatCompactDouble(result.MaximumCalibratedGateMomentValuePerIteration)}");
    }

    Console.WriteLine($"Total compressed records: {FormatNumber(result.TotalCompressedRecords)}");
    Console.WriteLine($"Total compressed bytes: {FormatNumber(result.TotalCompressedBytes)}");
    Console.WriteLine($"Total decompressed bytes: {FormatNumber(result.TotalDecompressedBytes)}");
    Console.WriteLine($"Total messages: {FormatNumber(result.TotalMessages)}");
    Console.WriteLine($"Total Type 31 radials: {FormatNumber(result.TotalType31Radials)}");
    Console.WriteLine($"Total estimated gate-moment events: {FormatNumber(result.TotalEstimatedGateMomentEvents)}");
    if (result.DecodeMomentValues)
    {
        Console.WriteLine($"Total decoded gate-moment values: {FormatNumber(result.TotalDecodedGateMomentValues)}");
    }

    if (result.DecodeCalibratedMomentValues)
    {
        Console.WriteLine($"Total calibrated gate-moment values: {FormatNumber(result.TotalCalibratedGateMomentValues)}");
        Console.WriteLine($"Total below-threshold gate-moment values: {FormatNumber(result.TotalBelowThresholdGateMomentValues)}");
        Console.WriteLine($"Total range-folded gate-moment values: {FormatNumber(result.TotalRangeFoldedGateMomentValues)}");
        Console.WriteLine($"Total CFP filter-not-applied values: {FormatNumber(result.TotalClutterFilterNotAppliedGateMomentValues)}");
        Console.WriteLine($"Total CFP point-clutter-filter values: {FormatNumber(result.TotalPointClutterFilterAppliedGateMomentValues)}");
        Console.WriteLine($"Total CFP dual-pol-filtered values: {FormatNumber(result.TotalDualPolarizationFilteredGateMomentValues)}");
        Console.WriteLine($"Total reserved gate-moment values: {FormatNumber(result.TotalReservedGateMomentValues)}");
        Console.WriteLine($"Total unsupported calibrated gate-moment values: {FormatNumber(result.TotalUnsupportedCalibratedGateMomentValues)}");
    }

    Console.WriteLine($"Elapsed ms: {FormatDecimal(result.Elapsed.TotalMilliseconds)}");
    Console.WriteLine($"Compressed MB/s: {FormatDecimal(MegabytesPerSecond(result.TotalCompressedBytes, result.Elapsed))}");
    Console.WriteLine($"Decompressed MB/s: {FormatDecimal(MegabytesPerSecond(result.TotalDecompressedBytes, result.Elapsed))}");
    Console.WriteLine($"Messages/s: {FormatDecimal(PerSecond(result.TotalMessages, result.Elapsed))}");
    Console.WriteLine($"Type 31 radials/s: {FormatDecimal(PerSecond(result.TotalType31Radials, result.Elapsed))}");
    Console.WriteLine($"Estimated gate-moment events/s: {FormatDecimal(PerSecond(result.TotalEstimatedGateMomentEvents, result.Elapsed))}");
    if (result.DecodeMomentValues)
    {
        Console.WriteLine($"Decoded gate-moment values/s: {FormatDecimal(PerSecond(result.TotalDecodedGateMomentValues, result.Elapsed))}");
    }

    if (result.DecodeCalibratedMomentValues)
    {
        Console.WriteLine($"Calibrated gate-moment values/s: {FormatDecimal(PerSecond(result.TotalCalibratedGateMomentValues, result.Elapsed))}");
    }

    Console.WriteLine($"Allocated bytes: {FormatNumber(result.AllocatedBytes)}");
    Console.WriteLine($"Allocated bytes / message: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalMessages, 1d))}");
    Console.WriteLine($"Allocated bytes / estimated event: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalEstimatedGateMomentEvents, 1d))}");
    if (result.DecodeMomentValues)
    {
        Console.WriteLine($"Allocated bytes / decoded value: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalDecodedGateMomentValues, 1d))}");
    }

    if (result.DecodeCalibratedMomentValues)
    {
        Console.WriteLine($"Allocated bytes / calibrated value: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalCalibratedGateMomentValues, 1d))}");
    }

    return 0;
}

static int BenchmarkArchiveReplayShape(string[] args)
{
    var options = ArchiveBenchmarkReplayShapeOptions.Parse(args);
    var result = new NexradArchiveReplayShapeBenchmark().Measure(
        options.FilePath,
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
    Console.WriteLine("Chronology verification: required");
    Console.WriteLine($"File size bytes: {FormatNumber(result.FileSizeBytes)}");
    Console.WriteLine($"Compressed records per iteration: {FormatNumber(result.CompressedRecordsPerIteration)}");
    Console.WriteLine($"Compressed bytes per iteration: {FormatNumber(result.CompressedBytesPerIteration)}");
    Console.WriteLine($"Decompressed bytes per iteration: {FormatNumber(result.DecompressedBytesPerIteration)}");
    Console.WriteLine($"Replay-shaped events per iteration: {FormatNumber(result.EventsPerIteration)}");
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
    Console.WriteLine($"Calibrated value range per iteration: {FormatCompactDouble(result.MinimumCalibratedValuePerIteration)}..{FormatCompactDouble(result.MaximumCalibratedValuePerIteration)}");
    Console.WriteLine($"Range km per iteration: {FormatCompactDouble(result.MinimumRangeKilometersPerIteration)}..{FormatCompactDouble(result.MaximumRangeKilometersPerIteration)}");
    Console.WriteLine($"Total compressed records: {FormatNumber(result.TotalCompressedRecords)}");
    Console.WriteLine($"Total compressed bytes: {FormatNumber(result.TotalCompressedBytes)}");
    Console.WriteLine($"Total decompressed bytes: {FormatNumber(result.TotalDecompressedBytes)}");
    Console.WriteLine($"Total replay-shaped events: {FormatNumber(result.TotalEvents)}");
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
    Console.WriteLine($"Replay-shaped events/s: {FormatDecimal(PerSecond(result.TotalEvents, result.Elapsed))}");
    Console.WriteLine($"Valid events/s: {FormatDecimal(PerSecond(result.TotalValidEvents, result.Elapsed))}");
    Console.WriteLine($"Allocated bytes: {FormatNumber(result.AllocatedBytes)}");
    Console.WriteLine($"Allocated bytes / event: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalEvents, 1d))}");
    Console.WriteLine($"Allocated bytes / valid event: {FormatDecimal(result.AllocatedBytes / Math.Max((double)result.TotalValidEvents, 1d))}");
    return 0;
}

static int BenchmarkArchiveReplayPublish(string[] args)
{
    var options = ArchiveBenchmarkReplayPublishOptions.Parse(args);
    if (options.FilePath is null)
    {
        var cacheResult = new NexradArchiveReplayPublishBenchmark().MeasureCache(
            options.CachePath ?? throw new InvalidOperationException("--cache is required when --file is not provided."),
            options.Date,
            options.RadarId,
            options.MaxFiles,
            options.Iterations,
            options.WarmupIterations,
            options.Parallelism,
            options.Decompressor,
            CancellationToken.None);
        PrintArchiveReplayPublishCacheBenchmarkResult(cacheResult);
        return 0;
    }

    var result = new NexradArchiveReplayPublishBenchmark().Measure(
        options.FilePath,
        options.Iterations,
        options.WarmupIterations,
        options.Parallelism,
        options.Decompressor,
        CancellationToken.None);
    PrintArchiveReplayPublishBenchmarkResult(result);
    return 0;
}

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

static void PrintArchiveReplayPublishBenchmarkResult(ArchiveReplayPublishBenchmarkResult result)
{
    Console.WriteLine($"File: {result.FilePath}");
    Console.WriteLine($"Decompressor: {result.Decompressor}");
    Console.WriteLine($"Iterations: {FormatNumber(result.Iterations)}");
    Console.WriteLine($"Warmup iterations: {FormatNumber(result.WarmupIterations)}");
    Console.WriteLine($"Parallelism: {FormatNumber(result.DegreeOfParallelism)}");
    Console.WriteLine("Chronology verification: required");
    Console.WriteLine($"File size bytes: {FormatNumber(result.FileSizeBytes)}");
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
}

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

static int ValidateArchive(string[] args)
{
    if (args.Length == 0)
    {
        return PrintUsage();
    }

    return args[0] switch
    {
        "decompress" => ValidateArchiveDecompression(args[1..]),
        "replay-shape" => ValidateArchiveReplayShape(args[1..]),
        _ => PrintUsage()
    };
}

static int ValidateArchiveDecompression(string[] args)
{
    var options = ArchiveValidateDecompressionOptions.Parse(args);
    var validator = new NexradArchiveDecompressionValidator();
    var result = options.FilePath is not null
        ? validator.ValidateFile(options.FilePath, CancellationToken.None)
        : validator.ValidateCache(
            options.CachePath ?? throw new InvalidOperationException("--cache is required when --file is not provided."),
            options.RadarId,
            options.MaxFiles,
            CancellationToken.None);

    Console.WriteLine($"Candidate decompressor: {result.CandidateDecompressor}");
    Console.WriteLine($"Reference decompressor: {result.ReferenceDecompressor}");
    Console.WriteLine($"Examined files: {FormatNumber(result.ExaminedFileCount)}");
    Console.WriteLine($"Skipped files: {FormatNumber(result.SkippedFileCount)}");
    Console.WriteLine($"Compared files: {FormatNumber(result.ComparedFileCount)}");
    Console.WriteLine($"Failed files: {FormatNumber(result.FailedFileCount)}");
    Console.WriteLine($"Compressed records: {FormatNumber(result.TotalCompressedRecordCount)}");
    Console.WriteLine($"Compressed bytes: {FormatNumber(result.TotalCompressedBytes)}");
    Console.WriteLine($"Decompressed bytes: {FormatNumber(result.TotalDecompressedBytes)}");

    foreach (var file in result.Files.Where(file => !file.Succeeded))
    {
        Console.WriteLine($"Failure: {file.FilePath}");
        Console.WriteLine($"Diagnostic: {file.Diagnostic}");
    }

    if (result.ComparedFileCount == 0)
    {
        Console.WriteLine("Diagnostic: no Archive Two base-data files were selected for validation.");
    }

    return result.Succeeded ? 0 : 1;
}

static int ValidateArchiveReplayShape(string[] args)
{
    var options = ArchiveValidateReplayShapeOptions.Parse(args);
    var validator = new NexradArchiveReplayShapeValidator(ArchiveBZip2Decompressors.Create(options.Decompressor));
    var result = options.FilePath is not null
        ? validator.ValidateFile(options.FilePath, options.Parallelism, CancellationToken.None)
        : validator.ValidateCache(
            options.CachePath ?? throw new InvalidOperationException("--cache is required when --file is not provided."),
            options.RadarId,
            options.MaxFiles,
            options.Parallelism,
            CancellationToken.None);

    Console.WriteLine($"Decompressor: {result.Decompressor}");
    Console.WriteLine($"Parallelism: {FormatNumber(result.DegreeOfParallelism)}");
    Console.WriteLine("Chronology verification: required");
    Console.WriteLine($"Examined files: {FormatNumber(result.ExaminedFileCount)}");
    Console.WriteLine($"Skipped files: {FormatNumber(result.SkippedFileCount)}");
    Console.WriteLine($"Compared files: {FormatNumber(result.ComparedFileCount)}");
    Console.WriteLine($"Failed files: {FormatNumber(result.FailedFileCount)}");
    Console.WriteLine($"Compressed records: {FormatNumber(result.TotalCompressedRecordCount)}");
    Console.WriteLine($"Compressed bytes: {FormatNumber(result.TotalCompressedBytes)}");
    Console.WriteLine($"Decompressed bytes: {FormatNumber(result.TotalDecompressedBytes)}");
    Console.WriteLine($"Replay-shaped events: {FormatNumber(result.TotalEvents)}");
    Console.WriteLine($"Valid events: {FormatNumber(result.TotalValidEvents)}");
    Console.WriteLine($"Valid event share: {FormatPercent(result.ValidEventShare)}");
    Console.WriteLine($"Below-threshold events: {FormatNumber(result.TotalBelowThresholdEvents)}");
    Console.WriteLine($"Range-folded events: {FormatNumber(result.TotalRangeFoldedEvents)}");
    Console.WriteLine($"CFP filter-not-applied events: {FormatNumber(result.TotalClutterFilterNotAppliedEvents)}");
    Console.WriteLine($"CFP point-clutter-filter events: {FormatNumber(result.TotalPointClutterFilterAppliedEvents)}");
    Console.WriteLine($"CFP dual-pol-filtered events: {FormatNumber(result.TotalDualPolarizationFilteredEvents)}");
    Console.WriteLine($"Reserved events: {FormatNumber(result.TotalReservedEvents)}");
    Console.WriteLine($"Unsupported events: {FormatNumber(result.TotalUnsupportedEvents)}");

    PrintReplayShapeUnevenness("Record valid-share spread", result.Files, file => file.RecordUnevenness);
    PrintReplayShapeUnevenness("Sweep valid-share spread", result.Files, file => file.SweepUnevenness);
    PrintReplayShapeUnevenness("Radial valid-share spread", result.Files, file => file.RadialUnevenness);
    PrintReplayShapeUnevenness("Minute valid-share spread", result.Files, file => file.TimeBucketUnevenness);

    foreach (var file in result.Files.Where(file => !file.Succeeded))
    {
        Console.WriteLine($"Failure: {file.FilePath}");
        Console.WriteLine($"Diagnostic: {file.Diagnostic}");
    }

    if (result.ComparedFileCount == 0)
    {
        Console.WriteLine("Diagnostic: no Archive Two base-data files were selected for replay-shape validation.");
    }

    return result.Succeeded ? 0 : 1;
}

static void PrintReplayShapeUnevenness(
    string label,
    IReadOnlyList<ArchiveTwoReplayShapeValidationFileResult> files,
    Func<ArchiveTwoReplayShapeValidationFileResult, ArchiveTwoReplayShapeUnevennessSummary> selectUnevenness)
{
    var rows = files
        .Where(file => file.Succeeded)
        .Select(file => new
        {
            File = file,
            Unevenness = selectUnevenness(file),
            Spread = selectUnevenness(file).MaximumValidShareBucket.ValidEventShare -
                selectUnevenness(file).MinimumValidShareBucket.ValidEventShare
        })
        .Where(row => row.Unevenness.BucketCount > 0)
        .OrderByDescending(row => row.Spread)
        .ThenBy(row => row.File.FilePath, StringComparer.Ordinal)
        .Take(5)
        .ToArray();

    if (rows.Length == 0)
    {
        return;
    }

    Console.WriteLine($"{label}:");
    foreach (var row in rows)
    {
        var min = row.Unevenness.MinimumValidShareBucket;
        var max = row.Unevenness.MaximumValidShareBucket;
        Console.WriteLine(
            $"  {Path.GetFileName(row.File.FilePath)}: " +
            $"{FormatNumber(row.Unevenness.BucketCount)} {row.Unevenness.BucketKind}s, " +
            $"min {row.Unevenness.BucketKind} {FormatNumber(min.BucketNumber)} " +
            $"{FormatPercent(min.ValidEventShare)} ({FormatNumber(min.ValidEvents)}/{FormatNumber(min.Events)}), " +
            $"max {row.Unevenness.BucketKind} {FormatNumber(max.BucketNumber)} " +
            $"{FormatPercent(max.ValidEventShare)} ({FormatNumber(max.ValidEvents)}/{FormatNumber(max.Events)}), " +
            $"spread {FormatPercent(row.Spread)}");
    }
}

static async Task<int> InspectArchiveAsync(string[] args)
{
    var options = ArchiveInspectOptions.Parse(args);
    if (options.FilePath is not null)
    {
        var inspection = await new NexradArchiveFileInspector().InspectAsync(options.FilePath, CancellationToken.None);
        PrintArchiveFileInspection(inspection);
        return 0;
    }

    var cacheInspection = await new NexradArchiveCacheInspector().InspectAsync(
        options.CachePath ?? throw new InvalidOperationException("--cache is required when --file is not provided."),
        options.Date,
        options.RadarId,
        options.MaxFiles,
        CancellationToken.None);
    PrintArchiveCacheInspection(cacheInspection);
    return 0;
}

static void PrintArchiveFileInspection(NexradArchiveFileInspection inspection)
{
    Console.WriteLine($"File: {inspection.FilePath}");
    Console.WriteLine($"Size bytes: {FormatNumber(inspection.SizeBytes)}");
    Console.WriteLine($"Kind: {FormatNexradArchiveFileKind(inspection.FileKind)}");
    if (inspection.ArchiveTwoVolumeHeader is { } header)
    {
        Console.WriteLine($"Archive filename: {header.ArchiveFilename}");
        Console.WriteLine($"Version: {header.Version}");
        Console.WriteLine($"Extension number: {header.ExtensionNumber}");
        Console.WriteLine($"Radar: {header.RadarId}");
        Console.WriteLine($"Volume time: {header.VolumeTimestamp:yyyy-MM-ddTHH:mm:ss.fffZ}");
    }

    if (inspection.CompressedRecords.Count > 0)
    {
        var compressedBytes = inspection.CompressedRecords.Sum(record => (long)record.CompressedSizeBytes);
        var recordsWithBZip2Signature = inspection.CompressedRecords.Count(record => record.StartsWithBZip2Signature);
        var decompressedRecordCount = inspection.CompressedRecords.Count(record => record.DecompressedSizeBytes is not null);
        var decompressedBytes = inspection.CompressedRecords.Sum(record => record.DecompressedSizeBytes ?? 0L);
        var recordsWithDecompressionDiagnostics = inspection.CompressedRecords.Count(record => record.DecompressionDiagnostic is not null);
        var firstRecord = inspection.CompressedRecords[0];
        Console.WriteLine($"Compressed records: {FormatNumber(inspection.CompressedRecords.Count)}");
        Console.WriteLine($"Compressed bytes: {FormatNumber(compressedBytes)}");
        Console.WriteLine($"Records with BZip2 signature: {FormatNumber(recordsWithBZip2Signature)}");
        Console.WriteLine($"Decompressed records: {FormatNumber(decompressedRecordCount)}");
        Console.WriteLine($"Decompressed bytes: {FormatNumber(decompressedBytes)}");
        Console.WriteLine($"Records with decompression diagnostics: {FormatNumber(recordsWithDecompressionDiagnostics)}");
        Console.WriteLine($"First record compressed bytes: {FormatNumber(firstRecord.CompressedSizeBytes)}");
        if (firstRecord.DecompressedSizeBytes is not null)
        {
            Console.WriteLine($"First record decompressed bytes: {FormatNumber(firstRecord.DecompressedSizeBytes.Value)}");
        }
    }

    if (inspection.MessageSummary is { MessageCount: > 0 } messages)
    {
        Console.WriteLine($"Messages: {FormatNumber(messages.MessageCount)}");
        Console.WriteLine("Message types: " + string.Join(", ", messages.MessageTypes.Select(type => $"{type.MessageType}={FormatNumber(type.Count)}")));
        Console.WriteLine($"Type 31 radials: {FormatNumber(messages.Type31.RadialCount)}");
        Console.WriteLine($"Estimated gate-moment events: {FormatNumber(messages.Type31.EstimatedGateMomentEventCount)}");
        if (messages.Type31.ConstantBlocks is { VolumeCount: > 0 } or { ElevationCount: > 0 } or { RadialCount: > 0 })
        {
            Console.WriteLine(
                "Type 31 constant blocks: " +
                $"VOL={FormatNumber(messages.Type31.ConstantBlocks.VolumeCount)}, " +
                $"ELV={FormatNumber(messages.Type31.ConstantBlocks.ElevationCount)}, " +
                $"RAD={FormatNumber(messages.Type31.ConstantBlocks.RadialCount)}");
        }

        if (messages.Type31.Moments.Count > 0)
        {
            Console.WriteLine("Moment calibration formula: value=(raw-offset)/scale");
            Console.WriteLine("Moments:");
            foreach (var moment in messages.Type31.Moments)
            {
                Console.WriteLine($"  {FormatMomentSummary(moment)}");
            }
        }

        if (messages.Type31.Sweeps.Count > 0)
        {
            Console.WriteLine($"Sweeps: {FormatNumber(messages.Type31.Sweeps.Count)}");
            foreach (var sweep in messages.Type31.Sweeps)
            {
                Console.WriteLine(
                    $"Sweep {FormatNumber(sweep.SequenceNumber)}: " +
                    $"elevation={FormatNumber(sweep.ElevationNumber)}, " +
                    $"cutSector={FormatCutSectorRange(sweep.MinimumCutSectorNumber, sweep.MaximumCutSectorNumber)}, " +
                    $"radials={FormatNumber(sweep.RadialCount)}, " +
                    $"angle={FormatDegrees(sweep.MinimumElevationAngleDegrees)}-{FormatDegrees(sweep.MaximumElevationAngleDegrees)} deg " +
                    $"avg={FormatDegrees(sweep.AverageElevationAngleDegrees)} deg, " +
                    $"status={FormatRadialStatus(sweep.StartRadialStatus)}->{FormatRadialStatus(sweep.EndRadialStatus)}, " +
                    $"source={FormatSourceOrder(sweep.FirstRadial)}->{FormatSourceOrder(sweep.LastRadial)}, " +
                    $"moments={FormatMomentNames(sweep.Moments)}");
            }
        }
    }

    if (!string.IsNullOrWhiteSpace(inspection.Diagnostic))
    {
        Console.WriteLine($"Diagnostic: {inspection.Diagnostic}");
    }
}

static void PrintArchiveCacheInspection(NexradArchiveCacheInspection inspection)
{
    Console.WriteLine($"Cache: {inspection.CachePath}");
    if (inspection.Date is { } date)
    {
        Console.WriteLine($"Date: {date:yyyy-MM-dd}");
    }

    if (inspection.RadarId is not null)
    {
        Console.WriteLine($"Radar: {inspection.RadarId}");
    }

    Console.WriteLine($"Examined files: {FormatNumber(inspection.ExaminedFileCount)}");
    Console.WriteLine($"Archive Two base-data files: {FormatNumber(inspection.ArchiveTwoBaseDataFileCount)}");
    Console.WriteLine($"MDM/compressed-stream files: {FormatNumber(inspection.MdmOrCompressedStreamFileCount)}");
    Console.WriteLine($"Unknown files: {FormatNumber(inspection.UnknownFileCount)}");
    Console.WriteLine($"Files with diagnostics: {FormatNumber(inspection.DiagnosticFileCount)}");
    Console.WriteLine($"Size bytes: {FormatNumber(inspection.TotalSizeBytes)}");
    Console.WriteLine($"Compressed records: {FormatNumber(inspection.TotalCompressedRecordCount)}");
    Console.WriteLine($"Compressed bytes: {FormatNumber(inspection.TotalCompressedBytes)}");
    Console.WriteLine($"Records with BZip2 signature: {FormatNumber(inspection.TotalRecordsWithBZip2Signature)}");
    Console.WriteLine($"Decompressed records: {FormatNumber(inspection.TotalDecompressedRecordCount)}");
    Console.WriteLine($"Decompressed bytes: {FormatNumber(inspection.TotalDecompressedBytes)}");
    Console.WriteLine($"Messages: {FormatNumber(inspection.TotalMessages)}");
    Console.WriteLine($"Type 31 radials: {FormatNumber(inspection.TotalType31Radials)}");
    Console.WriteLine($"Estimated gate-moment events: {FormatNumber(inspection.TotalEstimatedGateMomentEvents)}");

    if (inspection.Files.Count == 0)
    {
        Console.WriteLine("Diagnostic: no files matched the cache inspection filters.");
        return;
    }

    Console.WriteLine("Files:");
    foreach (var file in inspection.Files)
    {
        var compressedRecordCount = file.CompressedRecords.Count;
        var decompressedBytes = file.CompressedRecords.Sum(record => record.DecompressedSizeBytes ?? 0L);
        var messageCount = file.MessageSummary?.MessageCount ?? 0;
        var type31RadialCount = file.MessageSummary?.Type31.RadialCount ?? 0;
        var diagnostic = HasInspectionDiagnostic(file) ? ", diagnostic=yes" : string.Empty;
        Console.WriteLine(
            $"  {Path.GetFileName(file.FilePath)}: " +
            $"{FormatNexradArchiveFileKind(file.FileKind)}, " +
            $"records={FormatNumber(compressedRecordCount)}, " +
            $"decompressed={FormatNumber(decompressedBytes)}, " +
            $"messages={FormatNumber(messageCount)}, " +
            $"type31 radials={FormatNumber(type31RadialCount)}" +
            diagnostic);
    }
}

static bool HasInspectionDiagnostic(NexradArchiveFileInspection inspection) =>
    !string.IsNullOrWhiteSpace(inspection.Diagnostic) ||
    inspection.CompressedRecords.Any(record => !string.IsNullOrWhiteSpace(record.DecompressionDiagnostic));

static string FormatNexradArchiveFileKind(NexradArchiveFileKind fileClass) =>
    fileClass switch
    {
        NexradArchiveFileKind.ArchiveTwoBaseData => "Archive Two base data",
        NexradArchiveFileKind.MdmOrCompressedStream => "MDM or compressed stream",
        _ => "Unknown"
    };

static string FormatDegrees(float value) =>
    value.ToString("0.00", CultureInfo.InvariantCulture);

static string FormatSourceOrder(ArchiveTwoRadialSourceOrder sourceOrder) =>
    $"{FormatNumber(sourceOrder.CompressedRecordSequenceNumber)}/" +
    $"{FormatNumber(sourceOrder.MessageSequenceNumberInRecord)}/" +
    $"{FormatNumber(sourceOrder.Type31RadialSequenceNumber)}";

static string FormatMomentNames(IReadOnlyList<string> moments) =>
    moments.Count == 0
        ? "none"
        : string.Join(",", moments);

static string FormatMomentSummary(ArchiveTwoMomentSummary moment) =>
    $"{moment.Name}: {FormatNumber(moment.GateCount)} gates/{FormatNumber(moment.RadialCount)} radials, " +
    $"gates/radial={FormatIntRange(moment.MinimumGateCount, moment.MaximumGateCount)}, " +
    $"wordSize={FormatIntRange(moment.MinimumWordSizeBits, moment.MaximumWordSizeBits)} bits, " +
    $"firstGate={FormatFloatRange(moment.MinimumFirstGateRangeKilometers, moment.MaximumFirstGateRangeKilometers)} km, " +
    $"gateSpacing={FormatFloatRange(moment.MinimumGateSpacingKilometers, moment.MaximumGateSpacingKilometers)} km, " +
    $"scale={FormatFloatRange(moment.MinimumScale, moment.MaximumScale)}, " +
    $"offset={FormatFloatRange(moment.MinimumOffset, moment.MaximumOffset)}";

static string FormatIntRange(int minimum, int maximum) =>
    minimum == maximum
        ? FormatNumber(minimum)
        : $"{FormatNumber(minimum)}-{FormatNumber(maximum)}";

static string FormatFloatRange(float minimum, float maximum) =>
    MathF.Abs(minimum - maximum) < 0.0005f
        ? FormatCompactFloat(minimum)
        : $"{FormatCompactFloat(minimum)}-{FormatCompactFloat(maximum)}";

static string FormatCompactFloat(float value) =>
    value.ToString("0.###", CultureInfo.InvariantCulture);

static string FormatCompactDouble(double value) =>
    value.ToString("0.###", CultureInfo.InvariantCulture);

static string FormatCutSectorRange(int minimum, int maximum) =>
    minimum == maximum
        ? FormatNumber(minimum)
        : $"{FormatNumber(minimum)}-{FormatNumber(maximum)}";

static string FormatRadialStatus(int status) =>
    status switch
    {
        0 or 80 => $"start elevation ({status})",
        1 or 81 => $"intermediate ({status})",
        2 or 82 => $"end elevation ({status})",
        3 or 83 => $"start volume ({status})",
        4 or 84 => $"end volume ({status})",
        5 or 85 => $"start last elevation ({status})",
        _ => status.ToString(CultureInfo.InvariantCulture)
    };

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

internal sealed record ArchiveOptions(
    DateOnly? Date,
    IReadOnlyCollection<string> RadarIds,
    bool AllRadars,
    int? MaxFiles,
    long? MaxBytes,
    string? ManifestPath,
    string? OutputPath,
    int Concurrency)
{
    public static ArchiveOptions Parse(string[] args)
    {
        DateOnly? date = null;
        var radarIds = new List<string>();
        var allRadars = false;
        int? maxFiles = null;
        long? maxBytes = null;
        string? manifestPath = null;
        string? outputPath = null;
        var concurrency = 4;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarIds.Add(HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar")));
                    break;
                case "--all-radars":
                    allRadars = true;
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    break;
                case "--max-bytes":
                    maxBytes = long.Parse(RequireValue(args, ref i, "--max-bytes"));
                    break;
                case "--manifest":
                    manifestPath = RequireValue(args, ref i, "--manifest");
                    break;
                case "--output":
                    outputPath = RequireValue(args, ref i, "--output");
                    break;
                case "--concurrency":
                    concurrency = int.Parse(RequireValue(args, ref i, "--concurrency"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (concurrency <= 0)
        {
            throw new InvalidOperationException("--concurrency must be greater than zero.");
        }

        if (maxFiles is <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (maxBytes is <= 0)
        {
            throw new InvalidOperationException("--max-bytes must be greater than zero.");
        }

        return new ArchiveOptions(date, radarIds, allRadars, maxFiles, maxBytes, manifestPath, outputPath, concurrency);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ArchiveInspectOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles)
{
    public static ArchiveInspectOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null))
        {
            throw new InvalidOperationException("--date and --radar can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        return new ArchiveInspectOptions(filePath, cachePath, date, radarId, maxFiles);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ProcessingBenchmarkSyntheticOptions(
    RadarProcessingExecutionMode ExecutionMode,
    int SourceCount,
    int BatchCount,
    int EventsPerBatch,
    int PayloadValuesPerEvent,
    int PartitionCount,
    int ShardCount,
    RadarProcessingBenchmarkHandlerSet HandlerSet,
    int Iterations,
    int WarmupIterations,
    RadarProcessingAsyncExecutionOptions? AsyncExecution)
{
    public static ProcessingBenchmarkSyntheticOptions Parse(string[] args)
    {
        var executionMode = RadarProcessingExecutionMode.Sequential;
        var sourceCount = 16;
        var batchCount = 4;
        var eventsPerBatch = 1024;
        var payloadValuesPerEvent = 4;
        var partitionCount = 1;
        var shardCount = 1;
        var handlerSet = RadarProcessingBenchmarkHandlerSet.None;
        var iterations = 3;
        var warmupIterations = 1;
        int? workerCount = null;
        int? queueCapacity = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode":
                    executionMode = ParseExecutionMode(RequireValue(args, ref i, "--mode"));
                    break;
                case "--sources":
                    sourceCount = int.Parse(RequireValue(args, ref i, "--sources"));
                    break;
                case "--batches":
                    batchCount = int.Parse(RequireValue(args, ref i, "--batches"));
                    break;
                case "--events-per-batch":
                    eventsPerBatch = int.Parse(RequireValue(args, ref i, "--events-per-batch"));
                    break;
                case "--payload-values":
                    payloadValuesPerEvent = int.Parse(RequireValue(args, ref i, "--payload-values"));
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--workers":
                    workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                    break;
                case "--queue-capacity":
                    queueCapacity = int.Parse(RequireValue(args, ref i, "--queue-capacity"));
                    break;
                case "--handlers":
                    handlerSet = ParseHandlerSet(RequireValue(args, ref i, "--handlers"));
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        new RadarProcessingSyntheticWorkloadOptions(
            sourceCount,
            batchCount,
            eventsPerBatch,
            payloadValuesPerEvent).Validate();

        if (partitionCount <= 0)
        {
            throw new InvalidOperationException("--partitions must be greater than zero.");
        }

        if (shardCount <= 0)
        {
            throw new InvalidOperationException("--shards must be greater than zero.");
        }

        if (partitionCount < shardCount)
        {
            throw new InvalidOperationException("--partitions must be greater than or equal to --shards.");
        }

        if (partitionCount > sourceCount)
        {
            throw new InvalidOperationException("--partitions must be less than or equal to --sources.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (workerCount.HasValue && workerCount.Value <= 0)
        {
            throw new InvalidOperationException("--workers must be greater than zero.");
        }

        if (queueCapacity.HasValue && queueCapacity.Value <= 0)
        {
            throw new InvalidOperationException("--queue-capacity must be greater than zero.");
        }

        RadarProcessingAsyncExecutionOptions? asyncExecution = null;
        if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            asyncExecution = new RadarProcessingAsyncExecutionOptions(
                workerCount: workerCount ?? shardCount,
                queueCapacity: queueCapacity ?? 1);
        }
        else if (workerCount.HasValue || queueCapacity.HasValue)
        {
            throw new InvalidOperationException("--workers and --queue-capacity require --mode async.");
        }

        return new ProcessingBenchmarkSyntheticOptions(
            executionMode,
            sourceCount,
            batchCount,
            eventsPerBatch,
            payloadValuesPerEvent,
            partitionCount,
            shardCount,
            handlerSet,
            iterations,
            warmupIterations,
            asyncExecution);
    }

    private static RadarProcessingExecutionMode ParseExecutionMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "sequential" => RadarProcessingExecutionMode.Sequential,
            "partitioned" or "partitioned-barrier" => RadarProcessingExecutionMode.PartitionedBarrier,
            "async" or "async-partitioned" or "async-shard" or "async-shard-transport" =>
                RadarProcessingExecutionMode.AsyncShardTransport,
            _ => throw new ArgumentException($"Unknown processing mode: {value}")
        };

    private static RadarProcessingBenchmarkHandlerSet ParseHandlerSet(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" => RadarProcessingBenchmarkHandlerSet.None,
            "counter-checksum" => RadarProcessingBenchmarkHandlerSet.CounterChecksum,
            _ => throw new ArgumentException($"Unknown processing benchmark handler set: {value}")
        };

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

public sealed record ProcessingBenchmarkQuarantineLifecycleOptionOverrides(
    int? QuarantineTtlEvaluations,
    int? SustainedCoolingSampleCount,
    double? MaterialPressureChangeThreshold)
{
    public static ProcessingBenchmarkQuarantineLifecycleOptionOverrides None { get; } = new(null, null, null);

    public bool HasOverrides =>
        QuarantineTtlEvaluations is not null ||
        SustainedCoolingSampleCount is not null ||
        MaterialPressureChangeThreshold is not null;

    public RadarProcessingQuarantineLifecycleOptions ApplyTo(
        RadarProcessingQuarantineLifecycleOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (!HasOverrides)
        {
            return baseline;
        }

        return new RadarProcessingQuarantineLifecycleOptions(
            QuarantineTtlEvaluations ?? baseline.QuarantineTtlEvaluations,
            SustainedCoolingSampleCount ?? baseline.SustainedCoolingSampleCount,
            MaterialPressureChangeThreshold ?? baseline.MaterialPressureChangeThreshold);
    }
}

public sealed record ProcessingBenchmarkRebalanceSyntheticOptions(
    IReadOnlyList<RadarProcessingSyntheticRebalanceWorkloadKind> Workloads,
    IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> Modes,
    RadarProcessingValidationProfile ValidationProfile,
    ProcessingBenchmarkQuarantineLifecycleOptionOverrides QuarantineLifecycleOverrides,
    int Iterations,
    int WarmupIterations,
    RadarProcessingExecutionMode ExecutionMode = RadarProcessingExecutionMode.PartitionedBarrier,
    RadarProcessingAsyncExecutionOptions? AsyncExecution = null)
{
    private const int DefaultAsyncWorkerCount = 2;
    private const int DefaultAsyncQueueCapacity = 1;

    private static readonly IReadOnlyList<RadarProcessingSyntheticRebalanceWorkloadKind> AllWorkloads =
        Array.AsReadOnly(
        [
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced,
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard,
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition,
            RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike,
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm,
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry,
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear,
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry,
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry,
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear,
            RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard,
            RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection,
            RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection,
            RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons,
            RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention
        ]);

    private static readonly IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> AllModes =
        Array.AsReadOnly(
        [
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession
        ]);

    public static ProcessingBenchmarkRebalanceSyntheticOptions Parse(string[] args)
    {
        IReadOnlyList<RadarProcessingSyntheticRebalanceWorkloadKind> workloads = Array.AsReadOnly(
        [
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced
        ]);
        IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> modes = AllModes;
        var validationProfile = RadarProcessingValidationProfile.Diagnostic;
        int? quarantineTtlEvaluations = null;
        int? sustainedCoolingSampleCount = null;
        double? materialPressureChangeThreshold = null;
        var iterations = 3;
        var warmupIterations = 1;
        var executionMode = RadarProcessingExecutionMode.PartitionedBarrier;
        int? workerCount = null;
        int? queueCapacity = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--workload":
                    workloads = ParseWorkload(RequireValue(args, ref i, "--workload"));
                    break;
                case "--mode":
                    modes = ParseMode(RequireValue(args, ref i, "--mode"));
                    break;
                case "--execution":
                    executionMode = ParseExecutionMode(RequireValue(args, ref i, "--execution"));
                    break;
                case "--workers":
                    workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                    break;
                case "--queue-capacity":
                    queueCapacity = int.Parse(RequireValue(args, ref i, "--queue-capacity"));
                    break;
                case "--validation-profile":
                    validationProfile = ParseValidationProfile(RequireValue(args, ref i, "--validation-profile"));
                    break;
                case "--quarantine-ttl":
                case "--quarantine-ttl-evaluations":
                    quarantineTtlEvaluations = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--quarantine-sustained-cooling-samples":
                case "--quarantine-sustained-cooling-sample-count":
                    sustainedCoolingSampleCount = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--quarantine-material-pressure-change":
                case "--quarantine-material-pressure-change-threshold":
                    materialPressureChangeThreshold = double.Parse(
                        RequireValue(args, ref i, args[i]),
                        CultureInfo.InvariantCulture);
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (workerCount.HasValue && workerCount.Value <= 0)
        {
            throw new InvalidOperationException("--workers must be greater than zero.");
        }

        if (queueCapacity.HasValue && queueCapacity.Value <= 0)
        {
            throw new InvalidOperationException("--queue-capacity must be greater than zero.");
        }

        RadarProcessingAsyncExecutionOptions? asyncExecution = null;
        if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            asyncExecution = new RadarProcessingAsyncExecutionOptions(
                workerCount: workerCount ?? DefaultAsyncWorkerCount,
                queueCapacity: queueCapacity ?? DefaultAsyncQueueCapacity);
        }
        else if (workerCount.HasValue || queueCapacity.HasValue)
        {
            throw new InvalidOperationException("--workers and --queue-capacity require --execution async.");
        }

        var quarantineLifecycleOverrides = new ProcessingBenchmarkQuarantineLifecycleOptionOverrides(
            quarantineTtlEvaluations,
            sustainedCoolingSampleCount,
            materialPressureChangeThreshold);
        _ = quarantineLifecycleOverrides.ApplyTo(RadarProcessingQuarantineLifecycleOptions.Default);

        return new ProcessingBenchmarkRebalanceSyntheticOptions(
            workloads,
            modes,
            validationProfile,
            quarantineLifecycleOverrides,
            iterations,
            warmupIterations,
            executionMode,
            asyncExecution);
    }

    private static IReadOnlyList<RadarProcessingSyntheticRebalanceWorkloadKind> ParseWorkload(string value) =>
        value.ToLowerInvariant() switch
        {
            "all" => AllWorkloads,
            "balanced" => Single(RadarProcessingSyntheticRebalanceWorkloadKind.Balanced),
            "hot-shard" or "sustained-hot" or "sustained-hot-shard" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard),
            "intrinsic-hot" or "intrinsic-hot-partition" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition),
            "oscillating" or "oscillating-spike" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike),
            "cooldown" or "cooldown-storm" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm),
            "ttl-retry" or "quarantine-ttl-retry" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry),
            "cooling-clear" or "sustained-cooling-clear" or "quarantine-cooling-clear" or
                "quarantine-sustained-cooling-clear" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear),
            "pressure-change-retry" or "quarantine-pressure-change-retry" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry),
            "retry-reentry" or "quarantine-retry-reentry" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry),
            "successful-relief-clear" or "relief-clear" or "quarantine-successful-relief-clear" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear),
            "long-no-hot" or "long-no-hot-shard" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard),
            "long-cooldown" or "long-cooldown-rejection" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection),
            "long-unsafe-target" or "long-unsafe-target-rejection" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection),
            "long-mixed" or "long-mixed-skipped" or "long-mixed-skipped-reasons" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons),
            "counters-only" or "counters-only-retention" =>
                Single(RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention),
            _ => throw new ArgumentException($"Unknown synthetic rebalance workload: {value}")
        };

    private static IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> ParseMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "all" => AllModes,
            "static" or "static-no-rebalance" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance),
            "sampling" or "sampling-only" or "pressure-sampling" or "pressure-sampling-only" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly),
            "rebalance" or "session" or "rebalance-session" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession),
            _ => throw new ArgumentException($"Unknown synthetic rebalance benchmark mode: {value}")
        };

    private static RadarProcessingExecutionMode ParseExecutionMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "sync" or "synchronous" or "partitioned" or "partitioned-barrier" =>
                RadarProcessingExecutionMode.PartitionedBarrier,
            "async" or "async-partitioned" or "async-shard" or "async-shard-transport" =>
                RadarProcessingExecutionMode.AsyncShardTransport,
            _ => throw new ArgumentException($"Unknown synthetic rebalance execution mode: {value}")
        };

    private static RadarProcessingValidationProfile ParseValidationProfile(string value) =>
        value.ToLowerInvariant() switch
        {
            "off" => RadarProcessingValidationProfile.Off,
            "essential" => RadarProcessingValidationProfile.Essential,
            "diagnostic" or "diagnostics" => RadarProcessingValidationProfile.Diagnostic,
            "benchmark" => RadarProcessingValidationProfile.Benchmark,
            _ => throw new ArgumentException($"Unknown synthetic rebalance validation profile: {value}")
        };

    private static IReadOnlyList<T> Single<T>(T value) => Array.AsReadOnly([value]);

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

public sealed record ProcessingBenchmarkArchiveRebalanceOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> Modes,
    int PartitionCount,
    int ShardCount,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor,
    RadarProcessingValidationProfile ValidationProfile,
    ProcessingBenchmarkQuarantineLifecycleOptionOverrides QuarantineLifecycleOverrides,
    RadarProcessingTelemetryRetentionOptions TelemetryRetention,
    RadarProcessingPressureSkewOptions PressureSkew,
    RadarProcessingArchiveProviderMode ProviderMode = RadarProcessingArchiveProviderMode.BlockingBorrowed,
    int ProviderQueueCapacity = 1,
    TimeSpan? ProviderQueueTimeout = null,
    RadarProcessingQueuedProviderOverlapMode ProviderOverlapMode = RadarProcessingQueuedProviderOverlapMode.None,
    RadarProcessingRetainedPayloadStrategy RetentionStrategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
    long? ProviderQueueRetainedPayloadBytes = null,
    TimeSpan OverlapConsumerDelay = default,
    ProcessingBenchmarkProviderQueueTelemetryOutput QueueTelemetryOutput =
        ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
    ProcessingBenchmarkProviderOverlapTelemetryOutput OverlapTelemetryOutput =
        ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary,
    RadarProcessingExecutionMode ExecutionMode = RadarProcessingExecutionMode.PartitionedBarrier,
    RadarProcessingAsyncExecutionOptions? AsyncExecution = null,
    ProcessingBenchmarkArchiveRebalanceOptionProvenance? OptionProvenance = null)
{
    public const int DefaultCandidateProviderQueueCapacity =
        RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity;
    public const long DefaultCandidateRetainedPayloadBytes =
        RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes;
    public const int DefaultRolloutWorkerCount = RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount;
    public const int DefaultRolloutProviderQueueCapacity = DefaultCandidateProviderQueueCapacity;
    public const long DefaultRolloutRetainedPayloadBytes = DefaultCandidateRetainedPayloadBytes;
    public const string NaturalDefaultCandidateEvidenceContour = "natural-default-candidate";
    public const string ControlledProofEvidenceContour = "controlled-proof";
    public const string NaturalOptInEvidenceContour = "natural-opt-in";
    public const string NotApplicableEvidenceContour = "not-applicable";
    public const string NaturalReadinessEvidenceScope = "natural-readiness";
    public const string ControlledMechanicsEvidenceScope = "controlled-mechanics-proof";
    public const string OptInDiagnosticEvidenceScope = "opt-in-diagnostic";
    public const string NotApplicableEvidenceScope = "not-applicable";

    public bool IsDefaultCandidateContour =>
        MatchesDefaultCandidateContour(
            ProviderMode,
            ProviderQueueCapacity,
            ProviderOverlapMode,
            RetentionStrategy,
            ProviderQueueRetainedPayloadBytes,
            OverlapConsumerDelay,
            QueueTelemetryOutput,
            OverlapTelemetryOutput,
            ExecutionMode);

    public bool IsControlledProviderOverlapProof =>
        ProviderMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
        ProviderOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer &&
        OverlapConsumerDelay > TimeSpan.Zero;

    public string ProviderOverlapEvidenceContour =>
        FormatProviderOverlapEvidenceContour(
            ProviderMode,
            ProviderOverlapMode,
            OverlapConsumerDelay,
            IsDefaultCandidateContour);

    public string ProviderOverlapEvidenceScope =>
        FormatProviderOverlapEvidenceScope(ProviderOverlapEvidenceContour);

    public ProcessingBenchmarkArchiveRebalanceOptionProvenance EffectiveOptionProvenance =>
        OptionProvenance ?? ProcessingBenchmarkArchiveRebalanceOptionProvenance.CurrentDefaults;

    public bool IsExplicitBlockingBorrowedFallback =>
        ProviderMode == RadarProcessingArchiveProviderMode.BlockingBorrowed &&
        EffectiveOptionProvenance.ProviderMode == ProcessingBenchmarkOptionValueSource.Explicit;

    public bool IsRolloutDefaultExpandedContour =>
        IsDefaultCandidateContour &&
        EffectiveOptionProvenance.ProviderMode == ProcessingBenchmarkOptionValueSource.RolloutDefault;

    public static bool MatchesDefaultCandidateContour(
        RadarProcessingArchiveProviderMode providerMode,
        int providerQueueCapacity,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? providerQueueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput,
        RadarProcessingExecutionMode executionMode) =>
        providerMode == RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode &&
        providerQueueCapacity == DefaultCandidateProviderQueueCapacity &&
        providerOverlapMode == RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode &&
        retentionStrategy == RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy &&
        providerQueueRetainedPayloadBytes == DefaultCandidateRetainedPayloadBytes &&
        overlapConsumerDelay == RadarProcessingArchiveRebalanceRolloutDefaults.OverlapConsumerDelay &&
        queueTelemetryOutput != ProcessingBenchmarkProviderQueueTelemetryOutput.None &&
        overlapTelemetryOutput != ProcessingBenchmarkProviderOverlapTelemetryOutput.None &&
        executionMode == RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode;

    public static string FormatProviderOverlapEvidenceContour(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        TimeSpan overlapConsumerDelay,
        bool isDefaultCandidateContour)
    {
        if (overlapConsumerDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapConsumerDelay));
        }

        if (isDefaultCandidateContour)
        {
            return NaturalDefaultCandidateEvidenceContour;
        }

        return providerMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
            providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer
            ? overlapConsumerDelay > TimeSpan.Zero
                ? ControlledProofEvidenceContour
                : NaturalOptInEvidenceContour
            : NotApplicableEvidenceContour;
    }

    public static string FormatProviderOverlapEvidenceScope(string providerOverlapEvidenceContour)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerOverlapEvidenceContour);

        return providerOverlapEvidenceContour switch
        {
            NaturalDefaultCandidateEvidenceContour => NaturalReadinessEvidenceScope,
            ControlledProofEvidenceContour => ControlledMechanicsEvidenceScope,
            NaturalOptInEvidenceContour => OptInDiagnosticEvidenceScope,
            NotApplicableEvidenceContour => NotApplicableEvidenceScope,
            _ => throw new ArgumentException(
                "Unknown provider overlap evidence contour.",
                nameof(providerOverlapEvidenceContour))
        };
    }

    public static ProcessingBenchmarkArchiveRebalanceOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> modes = Array.AsReadOnly(
        [
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession
        ]);
        var partitionCount = 24;
        var shardCount = 4;
        var iterations = 1;
        var warmupIterations = 0;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        var validationProfile = RadarProcessingValidationProfile.Diagnostic;
        int? quarantineTtlEvaluations = null;
        int? sustainedCoolingSampleCount = null;
        double? materialPressureChangeThreshold = null;
        var retentionMode = RadarProcessingTelemetryRetentionOptions.Default.RetentionMode;
        var maxRetainedDecisions = RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedDecisions;
        var maxRetainedTransitions = RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedLifecycleTransitions;
        var maxRetainedAcceptedMoves = RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedAcceptedMoves;
        var maxRetainedValidationFailures =
            RadarProcessingTelemetryRetentionOptions.Default.MaxRetainedValidationFailures;
        var skewProfile = RadarProcessingPressureSkewOptions.None.Profile;
        var skewFactor = RadarProcessingPressureSkewOptions.None.Factor;
        var skewPeriod = RadarProcessingPressureSkewOptions.None.Period;
        var providerMode = RadarProcessingArchiveProviderMode.BlockingBorrowed;
        var providerModeWasProvided = false;
        var providerOverlapMode = RadarProcessingQueuedProviderOverlapMode.None;
        var providerOverlapModeWasProvided = false;
        var retentionStrategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy;
        var retentionStrategyWasProvided = false;
        long? queueRetainedPayloadBytes = null;
        var queueRetainedPayloadBytesWasProvided = false;
        TimeSpan? queueTimeout = null;
        var queueTelemetryOutput = ProcessingBenchmarkProviderQueueTelemetryOutput.Summary;
        var queueTelemetryWasProvided = false;
        var overlapTelemetryOutput = ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary;
        var overlapTelemetryWasProvided = false;
        var overlapConsumerDelay = TimeSpan.Zero;
        var overlapConsumerDelayWasProvided = false;
        var executionMode = RadarProcessingExecutionMode.PartitionedBarrier;
        var executionModeWasProvided = false;
        int? workerCount = null;
        var workerCountWasProvided = false;
        int? queueCapacity = null;
        var queueCapacityWasProvided = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--mode":
                    modes = ParseMode(RequireValue(args, ref i, "--mode"));
                    break;
                case "--provider":
                    providerMode = ParseProviderMode(RequireValue(args, ref i, "--provider"));
                    providerModeWasProvided = true;
                    break;
                case "--provider-overlap":
                    providerOverlapMode = ParseProviderOverlapMode(RequireValue(args, ref i, "--provider-overlap"));
                    providerOverlapModeWasProvided = true;
                    break;
                case "--retention-strategy":
                    retentionStrategy = ParseRetentionStrategy(RequireValue(args, ref i, "--retention-strategy"));
                    retentionStrategyWasProvided = true;
                    break;
                case "--execution":
                    executionMode = ParseExecutionMode(RequireValue(args, ref i, "--execution"));
                    executionModeWasProvided = true;
                    break;
                case "--workers":
                    workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                    workerCountWasProvided = true;
                    break;
                case "--queue-capacity":
                    queueCapacity = int.Parse(RequireValue(args, ref i, "--queue-capacity"));
                    queueCapacityWasProvided = true;
                    break;
                case "--queue-timeout-ms":
                    queueTimeout = TimeSpan.FromMilliseconds(
                        double.Parse(RequireValue(args, ref i, "--queue-timeout-ms"), CultureInfo.InvariantCulture));
                    break;
                case "--queue-retained-bytes":
                    queueRetainedPayloadBytes = long.Parse(RequireValue(args, ref i, "--queue-retained-bytes"));
                    queueRetainedPayloadBytesWasProvided = true;
                    break;
                case "--queue-telemetry":
                    queueTelemetryOutput = ParseQueueTelemetryOutput(RequireValue(args, ref i, "--queue-telemetry"));
                    queueTelemetryWasProvided = true;
                    break;
                case "--overlap-telemetry":
                    overlapTelemetryOutput = ParseOverlapTelemetryOutput(
                        RequireValue(args, ref i, "--overlap-telemetry"));
                    overlapTelemetryWasProvided = true;
                    break;
                case "--overlap-consumer-delay-ms":
                    overlapConsumerDelay = TimeSpan.FromMilliseconds(
                        double.Parse(
                            RequireValue(args, ref i, "--overlap-consumer-delay-ms"),
                            CultureInfo.InvariantCulture));
                    overlapConsumerDelayWasProvided = true;
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                case "--validation-profile":
                    validationProfile = ParseValidationProfile(RequireValue(args, ref i, "--validation-profile"));
                    break;
                case "--quarantine-ttl":
                case "--quarantine-ttl-evaluations":
                    quarantineTtlEvaluations = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--quarantine-sustained-cooling-samples":
                case "--quarantine-sustained-cooling-sample-count":
                    sustainedCoolingSampleCount = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--quarantine-material-pressure-change":
                case "--quarantine-material-pressure-change-threshold":
                    materialPressureChangeThreshold = double.Parse(
                        RequireValue(args, ref i, args[i]),
                        CultureInfo.InvariantCulture);
                    break;
                case "--retention-mode":
                    retentionMode = ParseRetentionMode(RequireValue(args, ref i, "--retention-mode"));
                    break;
                case "--max-retained-decisions":
                    maxRetainedDecisions = int.Parse(RequireValue(args, ref i, "--max-retained-decisions"));
                    break;
                case "--max-retained-transitions":
                case "--max-retained-lifecycle-transitions":
                    maxRetainedTransitions = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--max-retained-accepted-moves":
                    maxRetainedAcceptedMoves = int.Parse(RequireValue(args, ref i, "--max-retained-accepted-moves"));
                    break;
                case "--max-retained-validation-failures":
                    maxRetainedValidationFailures = int.Parse(
                        RequireValue(args, ref i, "--max-retained-validation-failures"));
                    break;
                case "--skew-profile":
                case "--pressure-skew-profile":
                    skewProfile = ParsePressureSkewProfile(RequireValue(args, ref i, args[i]));
                    break;
                case "--skew-factor":
                case "--pressure-skew-factor":
                    skewFactor = double.Parse(
                        RequireValue(args, ref i, args[i]),
                        CultureInfo.InvariantCulture);
                    break;
                case "--skew-period":
                case "--pressure-skew-period":
                    skewPeriod = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        var providerModeSource = CurrentDefaultOrExplicit(providerModeWasProvided);
        var providerOverlapModeSource = CurrentDefaultOrExplicit(providerOverlapModeWasProvided);
        var retentionStrategySource = CurrentDefaultOrExplicit(retentionStrategyWasProvided);
        var queueCapacitySource = CurrentDefaultOrExplicit(queueCapacityWasProvided);
        var queueRetainedPayloadBytesSource = CurrentDefaultOrExplicit(queueRetainedPayloadBytesWasProvided);
        var queueTelemetrySource = CurrentDefaultOrExplicit(queueTelemetryWasProvided);
        var overlapTelemetrySource = CurrentDefaultOrExplicit(overlapTelemetryWasProvided);
        var overlapConsumerDelaySource = CurrentDefaultOrExplicit(overlapConsumerDelayWasProvided);
        var executionModeSource = CurrentDefaultOrExplicit(executionModeWasProvided);
        var workerCountSource = CurrentDefaultOrExplicit(workerCountWasProvided);

        if (!providerModeWasProvided)
        {
            providerMode = RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode;
            providerModeSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;

            if (!providerOverlapModeWasProvided)
            {
                providerOverlapMode = RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode;
                providerOverlapModeSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!retentionStrategyWasProvided)
            {
                retentionStrategy = RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy;
                retentionStrategySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!queueCapacityWasProvided)
            {
                queueCapacity = DefaultRolloutProviderQueueCapacity;
                queueCapacitySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!queueRetainedPayloadBytesWasProvided)
            {
                queueRetainedPayloadBytes = DefaultRolloutRetainedPayloadBytes;
                queueRetainedPayloadBytesSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!queueTelemetryWasProvided)
            {
                queueTelemetrySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!overlapTelemetryWasProvided)
            {
                overlapTelemetrySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!overlapConsumerDelayWasProvided)
            {
                overlapConsumerDelaySource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!executionModeWasProvided)
            {
                executionMode = RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode;
                executionModeSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }

            if (!workerCountWasProvided &&
                executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
            {
                workerCount = DefaultRolloutWorkerCount;
                workerCountSource = ProcessingBenchmarkOptionValueSource.RolloutDefault;
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (partitionCount <= 0)
        {
            throw new InvalidOperationException("--partitions must be greater than zero.");
        }

        if (shardCount <= 0)
        {
            throw new InvalidOperationException("--shards must be greater than zero.");
        }

        if (partitionCount < shardCount)
        {
            throw new InvalidOperationException("--partitions must be greater than or equal to --shards.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        if (workerCount.HasValue && workerCount.Value <= 0)
        {
            throw new InvalidOperationException("--workers must be greater than zero.");
        }

        if (queueCapacity.HasValue && queueCapacity.Value <= 0)
        {
            throw new InvalidOperationException("--queue-capacity must be greater than zero.");
        }

        if (queueTimeout.HasValue &&
            queueTimeout.Value <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("--queue-timeout-ms must be greater than zero.");
        }

        if (queueTimeout.HasValue &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("--queue-timeout-ms requires --provider queued-owned.");
        }

        if (queueRetainedPayloadBytes.HasValue &&
            queueRetainedPayloadBytes.Value <= 0)
        {
            throw new InvalidOperationException("--queue-retained-bytes must be greater than zero.");
        }

        if (queueRetainedPayloadBytes.HasValue &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("--queue-retained-bytes requires --provider queued-owned.");
        }

        if (providerOverlapMode != RadarProcessingQueuedProviderOverlapMode.None &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("--provider-overlap requires --provider queued-owned.");
        }

        if (retentionStrategyWasProvided &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("--retention-strategy requires --provider queued-owned.");
        }

        if (providerMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
            retentionStrategy == RadarProcessingRetainedPayloadStrategy.BuilderTransfer)
        {
            throw new InvalidOperationException("--retention-strategy builder-transfer is not supported yet.");
        }

        if (overlapTelemetryWasProvided &&
            overlapTelemetryOutput != ProcessingBenchmarkProviderOverlapTelemetryOutput.None &&
            providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.None)
        {
            throw new InvalidOperationException("--overlap-telemetry requires --provider-overlap producer-consumer.");
        }

        if (overlapConsumerDelayWasProvided &&
            overlapConsumerDelay <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("--overlap-consumer-delay-ms must be greater than zero.");
        }

        if (overlapConsumerDelayWasProvided &&
            (providerMode != RadarProcessingArchiveProviderMode.QueuedOwned ||
             providerOverlapMode != RadarProcessingQueuedProviderOverlapMode.ProducerConsumer))
        {
            throw new InvalidOperationException(
                "--overlap-consumer-delay-ms requires --provider queued-owned --provider-overlap producer-consumer.");
        }

        RadarProcessingAsyncExecutionOptions? asyncExecution = null;
        if (executionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            asyncExecution = new RadarProcessingAsyncExecutionOptions(
                workerCount: workerCount ?? shardCount,
                queueCapacity: queueCapacity ?? 1);
        }
        else if (workerCount.HasValue)
        {
            throw new InvalidOperationException("--workers and --queue-capacity require --execution async.");
        }
        else if (queueCapacity.HasValue &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("--queue-capacity requires --execution async or --provider queued-owned.");
        }

        var providerQueueCapacity = providerMode == RadarProcessingArchiveProviderMode.QueuedOwned
            ? queueCapacity ?? 1
            : 1;

        ArchiveBZip2Decompressors.Create(decompressor);
        var telemetryRetention = new RadarProcessingTelemetryRetentionOptions(
            retentionMode,
            maxRetainedDecisions,
            maxRetainedTransitions,
            maxRetainedAcceptedMoves,
            maxRetainedValidationFailures);
        var pressureSkew = new RadarProcessingPressureSkewOptions(
            skewProfile,
            skewFactor,
            skewPeriod);
        var quarantineLifecycleOverrides = new ProcessingBenchmarkQuarantineLifecycleOptionOverrides(
            quarantineTtlEvaluations,
            sustainedCoolingSampleCount,
            materialPressureChangeThreshold);
        _ = quarantineLifecycleOverrides.ApplyTo(RadarProcessingQuarantineLifecycleOptions.Default);

        return new ProcessingBenchmarkArchiveRebalanceOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            modes,
            partitionCount,
            shardCount,
            iterations,
            warmupIterations,
            parallelism,
            decompressor,
            validationProfile,
            quarantineLifecycleOverrides,
            telemetryRetention,
            pressureSkew,
            providerMode,
            providerQueueCapacity,
            queueTimeout,
            providerOverlapMode,
            retentionStrategy,
            queueRetainedPayloadBytes,
            overlapConsumerDelay,
            queueTelemetryOutput,
            overlapTelemetryOutput,
            executionMode,
            asyncExecution,
            new ProcessingBenchmarkArchiveRebalanceOptionProvenance(
                providerModeSource,
                providerOverlapModeSource,
                retentionStrategySource,
                queueCapacitySource,
                queueRetainedPayloadBytesSource,
                queueTelemetrySource,
                overlapTelemetrySource,
                overlapConsumerDelaySource,
                executionModeSource,
                workerCountSource));
    }

    private static ProcessingBenchmarkOptionValueSource CurrentDefaultOrExplicit(bool wasProvided) =>
        wasProvided
            ? ProcessingBenchmarkOptionValueSource.Explicit
            : ProcessingBenchmarkOptionValueSource.CurrentDefault;

    private static IReadOnlyList<RadarProcessingSyntheticRebalanceBenchmarkMode> ParseMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "all" => Array.AsReadOnly(
            [
                RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance,
                RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly,
                RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession
            ]),
            "static" or "static-no-rebalance" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance),
            "sampling" or "sampling-only" or "pressure-sampling" or "pressure-sampling-only" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly),
            "rebalance" or "session" or "rebalance-session" =>
                Single(RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession),
            _ => throw new ArgumentException($"Unknown archive rebalance benchmark mode: {value}")
        };

    private static RadarProcessingDiagnosticRetentionMode ParseRetentionMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "counters" or "counter" or "counters-only" =>
                RadarProcessingDiagnosticRetentionMode.Counters,
            "recent" or "recent-detail" =>
                RadarProcessingDiagnosticRetentionMode.Recent,
            "diagnostic" or "diagnostics" =>
                RadarProcessingDiagnosticRetentionMode.Diagnostic,
            _ => throw new ArgumentException($"Unknown archive rebalance telemetry retention mode: {value}")
        };

    private static RadarProcessingExecutionMode ParseExecutionMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "sync" or "synchronous" or "partitioned" or "partitioned-barrier" =>
                RadarProcessingExecutionMode.PartitionedBarrier,
            "async" or "async-partitioned" or "async-shard" or "async-shard-transport" =>
                RadarProcessingExecutionMode.AsyncShardTransport,
            _ => throw new ArgumentException($"Unknown archive rebalance execution mode: {value}")
        };

    private static RadarProcessingArchiveProviderMode ParseProviderMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "blocking" or "borrowed" or "blocking-borrowed" =>
                RadarProcessingArchiveProviderMode.BlockingBorrowed,
            "queued" or "owned" or "queued-owned" =>
                RadarProcessingArchiveProviderMode.QueuedOwned,
            _ => throw new ArgumentException($"Unknown archive rebalance provider mode: {value}")
        };

    private static RadarProcessingQueuedProviderOverlapMode ParseProviderOverlapMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => RadarProcessingQueuedProviderOverlapMode.None,
            "producer-consumer" or "producerconsumer" or "overlap" =>
                RadarProcessingQueuedProviderOverlapMode.ProducerConsumer,
            _ => throw new ArgumentException($"Unknown archive rebalance provider overlap mode: {value}")
        };

    private static RadarProcessingRetainedPayloadStrategy ParseRetentionStrategy(string value) =>
        value.ToLowerInvariant() switch
        {
            "snapshot" or "snapshot-copy" =>
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            "pooled" or "pooled-copy" =>
                RadarProcessingRetainedPayloadStrategy.PooledCopy,
            "builder" or "builder-transfer" =>
                RadarProcessingRetainedPayloadStrategy.BuilderTransfer,
            _ => throw new ArgumentException($"Unknown archive rebalance retention strategy: {value}")
        };

    private static ProcessingBenchmarkProviderQueueTelemetryOutput ParseQueueTelemetryOutput(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => ProcessingBenchmarkProviderQueueTelemetryOutput.None,
            "summary" => ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
            "recent" or "details" => ProcessingBenchmarkProviderQueueTelemetryOutput.Recent,
            _ => throw new ArgumentException($"Unknown archive rebalance queue telemetry mode: {value}")
        };

    private static ProcessingBenchmarkProviderOverlapTelemetryOutput ParseOverlapTelemetryOutput(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => ProcessingBenchmarkProviderOverlapTelemetryOutput.None,
            "summary" => ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary,
            "recent" or "details" => ProcessingBenchmarkProviderOverlapTelemetryOutput.Recent,
            _ => throw new ArgumentException($"Unknown archive rebalance overlap telemetry mode: {value}")
        };

    private static RadarProcessingValidationProfile ParseValidationProfile(string value) =>
        value.ToLowerInvariant() switch
        {
            "off" => RadarProcessingValidationProfile.Off,
            "essential" => RadarProcessingValidationProfile.Essential,
            "diagnostic" or "diagnostics" => RadarProcessingValidationProfile.Diagnostic,
            "benchmark" => RadarProcessingValidationProfile.Benchmark,
            _ => throw new ArgumentException($"Unknown archive rebalance validation profile: {value}")
        };

    private static RadarProcessingPressureSkewProfile ParsePressureSkewProfile(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => RadarProcessingPressureSkewProfile.None,
            "hot-shard" => RadarProcessingPressureSkewProfile.HotShard,
            "rotating-hot-shard" or "rotating-shard" =>
                RadarProcessingPressureSkewProfile.RotatingHotShard,
            "hot-partition" => RadarProcessingPressureSkewProfile.HotPartition,
            "target-starvation" or "no-cold-target" =>
                RadarProcessingPressureSkewProfile.TargetStarvation,
            "budget-storm" => RadarProcessingPressureSkewProfile.BudgetStorm,
            _ => throw new ArgumentException($"Unknown archive rebalance pressure skew profile: {value}")
        };

    private static IReadOnlyList<T> Single<T>(T value) => Array.AsReadOnly([value]);

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

public sealed record ProcessingBenchmarkOrderedArchiveProcessingOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    int PartitionCount,
    int ShardCount,
    int ActiveBatchCapacity,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor,
    ProcessingBenchmarkProviderQueueTelemetryOutput QueueTelemetryOutput =
        ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
    ProcessingBenchmarkProviderOverlapTelemetryOutput OverlapTelemetryOutput =
        ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary)
{
    public static ProcessingBenchmarkOrderedArchiveProcessingOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        var partitionCount = 24;
        var shardCount = 4;
        var activeBatchCapacity = RadarProcessingRuntimeArchiveBaseline.OrderedActiveBatchCapacity;
        var iterations = 1;
        var warmupIterations = 0;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        var queueTelemetryOutput = ProcessingBenchmarkProviderQueueTelemetryOutput.Summary;
        var overlapTelemetryOutput = ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--partitions":
                    partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                    break;
                case "--shards":
                    shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                    break;
                case "--active-batches":
                case "--active-batch-capacity":
                    activeBatchCapacity = int.Parse(RequireValue(args, ref i, args[i]));
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                case "--queue-telemetry":
                    queueTelemetryOutput = ParseQueueTelemetryOutput(RequireValue(args, ref i, "--queue-telemetry"));
                    break;
                case "--overlap-telemetry":
                    overlapTelemetryOutput = ParseOverlapTelemetryOutput(
                        RequireValue(args, ref i, "--overlap-telemetry"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (partitionCount <= 0)
        {
            throw new InvalidOperationException("--partitions must be greater than zero.");
        }

        if (shardCount <= 0)
        {
            throw new InvalidOperationException("--shards must be greater than zero.");
        }

        if (partitionCount < shardCount)
        {
            throw new InvalidOperationException("--partitions must be greater than or equal to --shards.");
        }

        if (activeBatchCapacity <= 0)
        {
            throw new InvalidOperationException("--active-batches must be greater than zero.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);
        _ = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity);

        return new ProcessingBenchmarkOrderedArchiveProcessingOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            partitionCount,
            shardCount,
            activeBatchCapacity,
            iterations,
            warmupIterations,
            parallelism,
            decompressor,
            queueTelemetryOutput,
            overlapTelemetryOutput);
    }

    private static ProcessingBenchmarkProviderQueueTelemetryOutput ParseQueueTelemetryOutput(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => ProcessingBenchmarkProviderQueueTelemetryOutput.None,
            "summary" => ProcessingBenchmarkProviderQueueTelemetryOutput.Summary,
            "recent" or "details" => ProcessingBenchmarkProviderQueueTelemetryOutput.Recent,
            _ => throw new ArgumentException($"Unknown ordered archive processing queue telemetry mode: {value}")
        };

    private static ProcessingBenchmarkProviderOverlapTelemetryOutput ParseOverlapTelemetryOutput(string value) =>
        value.ToLowerInvariant() switch
        {
            "none" or "off" => ProcessingBenchmarkProviderOverlapTelemetryOutput.None,
            "summary" => ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary,
            "recent" or "details" => ProcessingBenchmarkProviderOverlapTelemetryOutput.Recent,
            _ => throw new ArgumentException($"Unknown ordered archive processing overlap telemetry mode: {value}")
        };

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ArchiveBenchmarkDecompressionOptions(
    string FilePath,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor)
{
    public static ArchiveBenchmarkDecompressionOptions Parse(string[] args)
    {
        string? filePath = null;
        var iterations = 3;
        var warmupIterations = 1;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveBenchmarkDecompressionOptions(filePath, iterations, warmupIterations, parallelism, decompressor);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ArchiveBenchmarkParseOptions(
    string FilePath,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor,
    bool DecodeMomentValues,
    bool DecodeCalibratedMomentValues)
{
    public static ArchiveBenchmarkParseOptions Parse(string[] args)
    {
        string? filePath = null;
        var iterations = 3;
        var warmupIterations = 1;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        var decodeMomentValues = false;
        var decodeCalibratedMomentValues = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                case "--decode-moments":
                    decodeMomentValues = true;
                    break;
                case "--decode-calibrated-moments":
                    decodeMomentValues = true;
                    decodeCalibratedMomentValues = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveBenchmarkParseOptions(
            filePath,
            iterations,
            warmupIterations,
            parallelism,
            decompressor,
            decodeMomentValues,
            decodeCalibratedMomentValues);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ArchiveBenchmarkReplayShapeOptions(
    string FilePath,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor)
{
    public static ArchiveBenchmarkReplayShapeOptions Parse(string[] args)
    {
        string? filePath = null;
        var iterations = 3;
        var warmupIterations = 1;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveBenchmarkReplayShapeOptions(
            filePath,
            iterations,
            warmupIterations,
            parallelism,
            decompressor);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ArchiveBenchmarkReplayPublishOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor)
{
    public static ArchiveBenchmarkReplayPublishOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        var iterations = 3;
        var warmupIterations = 1;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveBenchmarkReplayPublishOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            iterations,
            warmupIterations,
            parallelism,
            decompressor);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ArchiveBenchmarkStreamOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor)
{
    public static ArchiveBenchmarkStreamOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        var iterations = 3;
        var warmupIterations = 1;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--iterations":
                    iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                    break;
                case "--warmup-iterations":
                    warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (iterations <= 0)
        {
            throw new InvalidOperationException("--iterations must be greater than zero.");
        }

        if (warmupIterations < 0)
        {
            throw new InvalidOperationException("--warmup-iterations cannot be negative.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveBenchmarkStreamOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            iterations,
            warmupIterations,
            parallelism,
            decompressor);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ArchiveReplayOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    int Parallelism,
    string Decompressor)
{
    public static ArchiveReplayOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        DateOnly? date = null;
        string? radarId = null;
        var maxFiles = 20;
        var maxFilesWasProvided = false;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    maxFilesWasProvided = true;
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) &&
            (date is not null || radarId is not null || maxFilesWasProvided))
        {
            throw new InvalidOperationException("--date, --radar, and --max-files can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveReplayOptions(
            filePath,
            cachePath,
            date,
            radarId,
            maxFiles,
            parallelism,
            decompressor);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ArchiveStreamOptions(
    string FilePath,
    int Parallelism,
    string Decompressor)
{
    public static ArchiveStreamOptions Parse(string[] args)
    {
        string? filePath = null;
        var parallelism = 1;
        var decompressor = ArchiveBZip2Decompressors.DefaultName;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveStreamOptions(filePath, parallelism, decompressor);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ArchiveValidateDecompressionOptions(
    string? FilePath,
    string? CachePath,
    string? RadarId,
    int MaxFiles)
{
    public static ArchiveValidateDecompressionOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        string? radarId = null;
        var maxFiles = 20;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) && radarId is not null)
        {
            throw new InvalidOperationException("--radar can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        return new ArchiveValidateDecompressionOptions(filePath, cachePath, radarId, maxFiles);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

internal sealed record ArchiveValidateReplayShapeOptions(
    string? FilePath,
    string? CachePath,
    string? RadarId,
    int MaxFiles,
    int Parallelism,
    string Decompressor)
{
    public static ArchiveValidateReplayShapeOptions Parse(string[] args)
    {
        string? filePath = null;
        string? cachePath = null;
        string? radarId = null;
        var maxFiles = int.MaxValue;
        var parallelism = Math.Max(1, Environment.ProcessorCount);
        var decompressor = ArchiveBZip2Decompressors.DefaultName;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = RequireValue(args, ref i, "--cache");
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                    break;
                case "--parallelism":
                    parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = RequireValue(args, ref i, "--decompressor");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath) == string.IsNullOrWhiteSpace(cachePath))
        {
            throw new InvalidOperationException("Provide exactly one of --file or --cache.");
        }

        if (!string.IsNullOrWhiteSpace(filePath) && radarId is not null)
        {
            throw new InvalidOperationException("--radar can only be used with --cache.");
        }

        if (maxFiles <= 0)
        {
            throw new InvalidOperationException("--max-files must be greater than zero.");
        }

        if (parallelism <= 0)
        {
            throw new InvalidOperationException("--parallelism must be greater than zero.");
        }

        ArchiveBZip2Decompressors.Create(decompressor);

        return new ArchiveValidateReplayShapeOptions(
            filePath,
            cachePath,
            radarId,
            maxFiles,
            parallelism,
            decompressor);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }
}

public enum ProcessingBenchmarkProviderQueueTelemetryOutput
{
    None = 1,
    Summary = 2,
    Recent = 3
}

public enum ProcessingBenchmarkProviderOverlapTelemetryOutput
{
    None = 1,
    Summary = 2,
    Recent = 3
}

public enum ProcessingBenchmarkOptionValueSource
{
    CurrentDefault = 0,
    Explicit = 1,
    RolloutDefault = 2
}

public sealed record ProcessingBenchmarkArchiveRebalanceOptionProvenance(
    ProcessingBenchmarkOptionValueSource ProviderMode = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource ProviderOverlapMode = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource RetentionStrategy = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource QueueCapacity = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource QueueRetainedPayloadBytes = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource QueueTelemetry = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource OverlapTelemetry = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource OverlapConsumerDelay = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource ExecutionMode = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource WorkerCount = ProcessingBenchmarkOptionValueSource.CurrentDefault)
{
    public static ProcessingBenchmarkArchiveRebalanceOptionProvenance CurrentDefaults { get; } = new();
}

