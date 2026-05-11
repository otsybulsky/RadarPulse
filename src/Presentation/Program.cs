using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

if (args.Length < 2 || args[0] != "archive")
{
    PrintUsage();
    return 2;
}

try
{
    return args[1] switch
    {
        "list" => await ListArchiveAsync(args[2..]),
        "download" => await DownloadArchiveAsync(args[2..]),
        "inspect" => await InspectArchiveAsync(args[2..]),
        "benchmark" => BenchmarkArchive(args[2..]),
        "validate" => ValidateArchive(args[2..]),
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

static string FormatDecimal(double value) => value.ToString("N2", CultureInfo.InvariantCulture).Replace(',', '_');

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
    Console.WriteLine("  radarpulse archive benchmark decompress --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
    Console.WriteLine("  radarpulse archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress] [--decode-moments] [--decode-calibrated-moments]");
    Console.WriteLine("  radarpulse archive validate decompress (--file path | --cache data/nexrad [--radar KTLX] [--max-files n])");
    return 2;
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
        _ => PrintUsage()
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

static int ValidateArchive(string[] args)
{
    if (args.Length == 0)
    {
        return PrintUsage();
    }

    return args[0] switch
    {
        "decompress" => ValidateArchiveDecompression(args[1..]),
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

static async Task<int> InspectArchiveAsync(string[] args)
{
    var options = ArchiveInspectOptions.Parse(args);
    var inspection = await new NexradArchiveFileInspector().InspectAsync(options.FilePath, CancellationToken.None);

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

    return 0;
}

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

internal sealed record ArchiveInspectOptions(string FilePath)
{
    public static ArchiveInspectOptions Parse(string[] args)
    {
        string? filePath = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = RequireValue(args, ref i, "--file");
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("--file is required.");
        }

        return new ArchiveInspectOptions(filePath);
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

