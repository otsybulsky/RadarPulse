using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;
using static CliFormat;

internal static partial class ArchiveCliApplication
{
    private static async Task<int> ListArchiveAsync(string[] args)
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

    private static async Task<int> DownloadArchiveAsync(string[] args)
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

    private static async Task<HistoricalArchiveManifest> LoadManifestForDownloadAsync(
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
