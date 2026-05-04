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
        "download" => DownloadNotYetImplemented(),
        _ => PrintUsage()
    };
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
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

static int DownloadNotYetImplemented()
{
    Console.Error.WriteLine("archive download is planned after manifest generation is stable.");
    return 2;
}

static int PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  radarpulse archive list --date yyyy-MM-dd --radar KTLX [--max-files n] [--max-bytes n] [--manifest path]");
    Console.WriteLine("  radarpulse archive list --date yyyy-MM-dd --all-radars [--max-files n] [--max-bytes n] [--manifest path]");
    Console.WriteLine("  radarpulse archive download --date yyyy-MM-dd --radar KTLX --output data/nexrad");
    return 2;
}

internal sealed record ArchiveOptions(
    DateOnly? Date,
    IReadOnlyCollection<string> RadarIds,
    bool AllRadars,
    int? MaxFiles,
    long? MaxBytes,
    string? ManifestPath)
{
    public static ArchiveOptions Parse(string[] args)
    {
        DateOnly? date = null;
        var radarIds = new List<string>();
        var allRadars = false;
        int? maxFiles = null;
        long? maxBytes = null;
        string? manifestPath = null;

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
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        return new ArchiveOptions(date, radarIds, allRadars, maxFiles, maxBytes, manifestPath);
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
