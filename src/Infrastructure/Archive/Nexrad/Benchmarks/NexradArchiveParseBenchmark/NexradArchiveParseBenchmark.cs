using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;


/// <summary>
/// Measures Archive II message parsing throughput, optional moment decoding, and allocation.
/// </summary>
public sealed partial class NexradArchiveParseBenchmark
{
    private const int OutputBufferSize = 81920;

    /// <summary>
    /// Measures message parsing without moment-value decoding.
    /// </summary>
    public ArchiveTwoParseBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        CancellationToken cancellationToken) =>
        Measure(
            filePath,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            decompressorName,
            decodeMomentValues: false,
            decodeCalibratedMomentValues: false,
            cancellationToken);

    /// <summary>
    /// Measures message parsing with optional raw moment-value decoding.
    /// </summary>
    public ArchiveTwoParseBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        bool decodeMomentValues,
        CancellationToken cancellationToken) =>
        Measure(
            filePath,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            decompressorName,
            decodeMomentValues,
            decodeCalibratedMomentValues: false,
            cancellationToken);

    /// <summary>
    /// Measures message parsing with optional raw and calibrated moment-value decoding.
    /// </summary>
    public ArchiveTwoParseBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        bool decodeMomentValues,
        bool decodeCalibratedMomentValues,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var decompressor = ArchiveBZip2Decompressors.Create(decompressorName);
        if (iterations <= 0)
        {
            throw new ArgumentException("Iterations must be greater than zero.", nameof(iterations));
        }

        if (warmupIterations < 0)
        {
            throw new ArgumentException("Warmup iterations cannot be negative.", nameof(warmupIterations));
        }

        if (degreeOfParallelism <= 0)
        {
            throw new ArgumentException("Degree of parallelism must be greater than zero.", nameof(degreeOfParallelism));
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        ArchiveTwoFileReader.ValidateVolumeHeaderSignature(fileInfo);
        var workers = CreateWorkers(
            decompressor,
            degreeOfParallelism,
            decodeMomentValues || decodeCalibratedMomentValues,
            decodeCalibratedMomentValues);
        try
        {
            for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MeasureIteration(fileInfo, degreeOfParallelism, workers, cancellationToken);
            }

            var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            ArchiveTwoParseIterationMeasurement? expectedIteration = null;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var iterationResult = MeasureIteration(fileInfo, degreeOfParallelism, workers, cancellationToken);
                expectedIteration ??= iterationResult;

                if (expectedIteration.Value != iterationResult)
                {
                    throw new InvalidDataException("Archive parse benchmark produced inconsistent iteration totals.");
                }
            }

            stopwatch.Stop();
            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
            var measurement = expectedIteration ?? default;

            return new ArchiveTwoParseBenchmarkResult(
                filePath,
                decompressor.Name,
                iterations,
                warmupIterations,
                degreeOfParallelism,
                decodeMomentValues || decodeCalibratedMomentValues,
                decodeCalibratedMomentValues,
                fileInfo.Length,
                measurement.CompressedRecordCount,
                measurement.CompressedBytes,
                measurement.DecompressedBytes,
                measurement.MessageCount,
                measurement.Type31RadialCount,
                measurement.EstimatedGateMomentEvents,
                measurement.DecodedGateMomentValues,
                measurement.DecodedGateMomentValueChecksum,
                measurement.CalibratedGateMomentValues,
                measurement.BelowThresholdGateMomentValues,
                measurement.RangeFoldedGateMomentValues,
                measurement.ClutterFilterNotAppliedGateMomentValues,
                measurement.PointClutterFilterAppliedGateMomentValues,
                measurement.DualPolarizationFilteredGateMomentValues,
                measurement.ReservedGateMomentValues,
                measurement.UnsupportedCalibratedGateMomentValues,
                measurement.CalibratedGateMomentValueScaledChecksum,
                measurement.MinimumCalibratedGateMomentValue,
                measurement.MaximumCalibratedGateMomentValue,
                stopwatch.Elapsed,
                allocatedBytes);
        }
        finally
        {
            foreach (var worker in workers)
            {
                worker.Dispose();
            }
        }
    }
}
