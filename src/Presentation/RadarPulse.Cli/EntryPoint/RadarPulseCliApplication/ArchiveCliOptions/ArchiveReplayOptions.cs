using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

internal sealed record ArchiveReplayOptions(
    string? FilePath,
    string? CachePath,
    DateOnly? Date,
    string? RadarId,
    int MaxFiles,
    int Parallelism,
    string Decompressor)
{
    /// <summary>
    /// Parses archive replay publishing options from CLI arguments.
    /// </summary>
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
                    maxFilesWasProvided = true;
                    break;
                case "--parallelism":
                    parallelism = int.Parse(ArchiveCliOptionReader.RequireValue(args, ref i, "--parallelism"));
                    break;
                case "--decompressor":
                    decompressor = ArchiveCliOptionReader.RequireValue(args, ref i, "--decompressor");
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
}
