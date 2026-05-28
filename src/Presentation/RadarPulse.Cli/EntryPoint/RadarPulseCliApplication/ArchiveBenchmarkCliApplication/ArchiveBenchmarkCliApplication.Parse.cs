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

internal static partial class ArchiveBenchmarkCliApplication
{
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
}
