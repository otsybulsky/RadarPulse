using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

internal sealed record ArchiveInspectOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles)
{
    /// <summary>
    /// Parses Archive II inspection options from CLI arguments.
    /// </summary>
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
                    filePath = ArchiveCliOptionReader.RequireValue(args, ref i, "--file");
                    break;
                case "--cache":
                    cachePath = ArchiveCliOptionReader.RequireValue(args, ref i, "--cache");
                    break;
                case "--date":
                    date = DateOnly.Parse(ArchiveCliOptionReader.RequireValue(args, ref i, "--date"));
                    break;
                case "--radar":
                    radarId = HistoricalArchiveRequest.NormalizeRadarId(
                        ArchiveCliOptionReader.RequireValue(args, ref i, "--radar"));
                    break;
                case "--max-files":
                    maxFiles = int.Parse(ArchiveCliOptionReader.RequireValue(args, ref i, "--max-files"));
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
}
