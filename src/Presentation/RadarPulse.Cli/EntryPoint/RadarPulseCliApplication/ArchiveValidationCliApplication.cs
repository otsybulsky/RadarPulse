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

internal static class ArchiveValidationCliApplication
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            return RadarPulseCliUsage.Print();
        }

        return args[0] switch
        {
            "decompress" => ValidateArchiveDecompression(args[1..]),
            "replay-shape" => ValidateArchiveReplayShape(args[1..]),
            _ => RadarPulseCliUsage.Print()
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

    static int ValidateArchiveReplayShape(string[] args)
    {
        var options = ArchiveValidateReplayShapeOptions.Parse(args);
        var validator = new NexradArchiveReplayShapeValidator(ArchiveBZip2Decompressors.Create(options.Decompressor));
        var result = options.FilePath is not null
            ? validator.ValidateFile(options.FilePath, options.Parallelism, CancellationToken.None)
            : validator.ValidateCache(
                options.CachePath ?? throw new InvalidOperationException("--cache is required when --file is not provided."),
                options.RadarId,
                options.MaxFiles,
                options.Parallelism,
                CancellationToken.None);

        Console.WriteLine($"Decompressor: {result.Decompressor}");
        Console.WriteLine($"Parallelism: {FormatNumber(result.DegreeOfParallelism)}");
        Console.WriteLine("Chronology verification: required");
        Console.WriteLine($"Examined files: {FormatNumber(result.ExaminedFileCount)}");
        Console.WriteLine($"Skipped files: {FormatNumber(result.SkippedFileCount)}");
        Console.WriteLine($"Compared files: {FormatNumber(result.ComparedFileCount)}");
        Console.WriteLine($"Failed files: {FormatNumber(result.FailedFileCount)}");
        Console.WriteLine($"Compressed records: {FormatNumber(result.TotalCompressedRecordCount)}");
        Console.WriteLine($"Compressed bytes: {FormatNumber(result.TotalCompressedBytes)}");
        Console.WriteLine($"Decompressed bytes: {FormatNumber(result.TotalDecompressedBytes)}");
        Console.WriteLine($"Replay-shaped events: {FormatNumber(result.TotalEvents)}");
        Console.WriteLine($"Valid events: {FormatNumber(result.TotalValidEvents)}");
        Console.WriteLine($"Valid event share: {FormatPercent(result.ValidEventShare)}");
        Console.WriteLine($"Below-threshold events: {FormatNumber(result.TotalBelowThresholdEvents)}");
        Console.WriteLine($"Range-folded events: {FormatNumber(result.TotalRangeFoldedEvents)}");
        Console.WriteLine($"CFP filter-not-applied events: {FormatNumber(result.TotalClutterFilterNotAppliedEvents)}");
        Console.WriteLine($"CFP point-clutter-filter events: {FormatNumber(result.TotalPointClutterFilterAppliedEvents)}");
        Console.WriteLine($"CFP dual-pol-filtered events: {FormatNumber(result.TotalDualPolarizationFilteredEvents)}");
        Console.WriteLine($"Reserved events: {FormatNumber(result.TotalReservedEvents)}");
        Console.WriteLine($"Unsupported events: {FormatNumber(result.TotalUnsupportedEvents)}");

        PrintReplayShapeUnevenness("Record valid-share spread", result.Files, file => file.RecordUnevenness);
        PrintReplayShapeUnevenness("Sweep valid-share spread", result.Files, file => file.SweepUnevenness);
        PrintReplayShapeUnevenness("Radial valid-share spread", result.Files, file => file.RadialUnevenness);
        PrintReplayShapeUnevenness("Minute valid-share spread", result.Files, file => file.TimeBucketUnevenness);

        foreach (var file in result.Files.Where(file => !file.Succeeded))
        {
            Console.WriteLine($"Failure: {file.FilePath}");
            Console.WriteLine($"Diagnostic: {file.Diagnostic}");
        }

        if (result.ComparedFileCount == 0)
        {
            Console.WriteLine("Diagnostic: no Archive Two base-data files were selected for replay-shape validation.");
        }

        return result.Succeeded ? 0 : 1;
    }

    static void PrintReplayShapeUnevenness(
        string label,
        IReadOnlyList<ArchiveTwoReplayShapeValidationFileResult> files,
        Func<ArchiveTwoReplayShapeValidationFileResult, ArchiveTwoReplayShapeUnevennessSummary> selectUnevenness)
    {
        var rows = files
            .Where(file => file.Succeeded)
            .Select(file => new
            {
                File = file,
                Unevenness = selectUnevenness(file),
                Spread = selectUnevenness(file).MaximumValidShareBucket.ValidEventShare -
                    selectUnevenness(file).MinimumValidShareBucket.ValidEventShare
            })
            .Where(row => row.Unevenness.BucketCount > 0)
            .OrderByDescending(row => row.Spread)
            .ThenBy(row => row.File.FilePath, StringComparer.Ordinal)
            .Take(5)
            .ToArray();

        if (rows.Length == 0)
        {
            return;
        }

        Console.WriteLine($"{label}:");
        foreach (var row in rows)
        {
            var min = row.Unevenness.MinimumValidShareBucket;
            var max = row.Unevenness.MaximumValidShareBucket;
            Console.WriteLine(
                $"  {Path.GetFileName(row.File.FilePath)}: " +
                $"{FormatNumber(row.Unevenness.BucketCount)} {row.Unevenness.BucketKind}s, " +
                $"min {row.Unevenness.BucketKind} {FormatNumber(min.BucketNumber)} " +
                $"{FormatPercent(min.ValidEventShare)} ({FormatNumber(min.ValidEvents)}/{FormatNumber(min.Events)}), " +
                $"max {row.Unevenness.BucketKind} {FormatNumber(max.BucketNumber)} " +
                $"{FormatPercent(max.ValidEventShare)} ({FormatNumber(max.ValidEvents)}/{FormatNumber(max.Events)}), " +
                $"spread {FormatPercent(row.Spread)}");
        }
    }

}
