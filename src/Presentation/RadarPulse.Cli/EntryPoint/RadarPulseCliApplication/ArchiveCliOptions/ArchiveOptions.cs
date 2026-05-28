using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

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
    /// <summary>
    /// Parses historical archive download options from CLI arguments.
    /// </summary>
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
                    date = DateOnly.Parse(ArchiveCliOptionReader.RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarIds.Add(HistoricalArchiveRequest.NormalizeRadarId(
                        ArchiveCliOptionReader.RequireValue(args, ref i, "--radar")));
                    break;
                case "--all-radars":
                    allRadars = true;
                    break;
                case "--max-files":
                    maxFiles = int.Parse(ArchiveCliOptionReader.RequireValue(args, ref i, "--max-files"));
                    break;
                case "--max-bytes":
                    maxBytes = long.Parse(ArchiveCliOptionReader.RequireValue(args, ref i, "--max-bytes"));
                    break;
                case "--manifest":
                    manifestPath = ArchiveCliOptionReader.RequireValue(args, ref i, "--manifest");
                    break;
                case "--output":
                    outputPath = ArchiveCliOptionReader.RequireValue(args, ref i, "--output");
                    break;
                case "--concurrency":
                    concurrency = int.Parse(ArchiveCliOptionReader.RequireValue(args, ref i, "--concurrency"));
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
}
