using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Measures Archive II replay-shape projection throughput and deterministic event totals.
/// </summary>
public sealed partial class NexradArchiveReplayShapeBenchmark
{
    private const int OutputBufferSize = 81920;

    /// <summary>
    /// Measures replay-shape projection with sequential processing.
    /// </summary>
    public ArchiveTwoReplayShapeBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        string decompressorName,
        CancellationToken cancellationToken) =>
        Measure(
            filePath,
            iterations,
            warmupIterations,
            degreeOfParallelism: 1,
            decompressorName,
            cancellationToken);

    /// <summary>
    /// Measures replay-shape projection with an explicit parallelism degree.
    /// </summary>
    public ArchiveTwoReplayShapeBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
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

        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var workers = CreateWorkers(
            decompressor,
            degreeOfParallelism,
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp);
        try
        {
            for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MeasureIteration(fileInfo, degreeOfParallelism, workers, cancellationToken);
            }

            var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            ArchiveTwoReplayShapeIterationMeasurement? expectedIteration = null;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var iterationResult = MeasureIteration(fileInfo, degreeOfParallelism, workers, cancellationToken);
                if (expectedIteration is null)
                {
                    expectedIteration = iterationResult;
                }
                else if (!expectedIteration.HasSameTotals(iterationResult))
                {
                    throw new InvalidDataException("Replay-shape benchmark produced inconsistent iteration totals.");
                }
            }

            stopwatch.Stop();
            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
            var measurement = expectedIteration ?? throw new InvalidOperationException("Replay-shape benchmark did not run any iterations.");

            return new ArchiveTwoReplayShapeBenchmarkResult(
                filePath,
                decompressor.Name,
                iterations,
                warmupIterations,
                degreeOfParallelism,
                fileInfo.Length,
                measurement.CompressedRecordCount,
                measurement.CompressedBytes,
                measurement.DecompressedBytes,
                measurement.Events,
                measurement.ValidEvents,
                measurement.BelowThresholdEvents,
                measurement.RangeFoldedEvents,
                measurement.ClutterFilterNotAppliedEvents,
                measurement.PointClutterFilterAppliedEvents,
                measurement.DualPolarizationFilteredEvents,
                measurement.ReservedEvents,
                measurement.UnsupportedEvents,
                measurement.RawValueChecksum,
                measurement.CalibratedValueScaledChecksum,
                measurement.ChronologyChecksum,
                measurement.MinimumCalibratedValue,
                measurement.MaximumCalibratedValue,
                measurement.MinimumRangeKilometers,
                measurement.MaximumRangeKilometers,
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
