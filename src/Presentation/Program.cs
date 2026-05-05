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
        _ => PrintUsage()
    };
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operation cancelled.");
    return 1;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or IOException)
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
    return 2;
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
        var firstRecord = inspection.CompressedRecords[0];
        Console.WriteLine($"Compressed records: {FormatNumber(inspection.CompressedRecords.Count)}");
        Console.WriteLine($"Compressed bytes: {FormatNumber(compressedBytes)}");
        Console.WriteLine($"Records with BZip2 signature: {FormatNumber(recordsWithBZip2Signature)}");
        Console.WriteLine($"First record compressed bytes: {FormatNumber(firstRecord.CompressedSizeBytes)}");
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

