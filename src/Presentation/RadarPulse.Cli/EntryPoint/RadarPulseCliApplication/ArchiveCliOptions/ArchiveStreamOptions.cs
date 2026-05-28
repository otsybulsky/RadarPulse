using RadarPulse.Infrastructure.Archive;

internal sealed record ArchiveStreamOptions(
    string FilePath,
    int Parallelism,
    string Decompressor)
{
    /// <summary>
    /// Parses Archive II to radar-event-batch stream options from CLI arguments.
    /// </summary>
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
                    filePath = ArchiveCliOptionReader.RequireValue(args, ref i, "--file");
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
}
