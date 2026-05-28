using RadarPulse.Infrastructure.Archive;

internal sealed record ArchiveBenchmarkParseOptions(
    string FilePath,
    int Iterations,
    int WarmupIterations,
    int Parallelism,
    string Decompressor,
    bool DecodeMomentValues,
    bool DecodeCalibratedMomentValues)
{
    /// <summary>
    /// Parses Archive II parse benchmark options from CLI arguments.
    /// </summary>
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
