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

internal static class ArchiveBenchmarkCliApplication
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            return RadarPulseCliUsage.Print();
        }

        return args[0] switch
        {
            "decompress" => BenchmarkArchiveDecompression(args[1..]),
            "parse" => BenchmarkArchiveParse(args[1..]),
            "replay-shape" => BenchmarkArchiveReplayShape(args[1..]),
            "replay-publish" => BenchmarkArchiveReplayPublish(args[1..]),
            "stream" => BenchmarkArchiveStream(args[1..]),
            _ => RadarPulseCliUsage.Print()
        };
    }

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

}
