using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveReplayPublishBenchmark
{
    private readonly IArchiveBZip2Decompressor decompressor;

    public NexradArchiveReplayPublishBenchmark()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    public NexradArchiveReplayPublishBenchmark(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    public ArchiveReplayPublishBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        CancellationToken cancellationToken) =>
        new NexradArchiveReplayPublishBenchmark(ArchiveBZip2Decompressors.Create(decompressorName))
            .Measure(filePath, iterations, warmupIterations, degreeOfParallelism, cancellationToken);

    public ArchiveReplayPublishBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
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

        using var session = new NexradArchiveReplayPublishSession(decompressor, degreeOfParallelism);
        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.PublishFile(fileInfo.FullName, cancellationToken);
        }

        var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ArchiveReplayPublishResult? expectedIteration = null;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationResult = session.PublishFile(fileInfo.FullName, cancellationToken);
            if (expectedIteration is null)
            {
                expectedIteration = iterationResult;
            }
            else if (!HasSameTotals(expectedIteration, iterationResult))
            {
                throw new InvalidDataException("Replay publish benchmark produced inconsistent iteration totals.");
            }
        }

        stopwatch.Stop();
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
        var measurement = expectedIteration ?? throw new InvalidOperationException("Replay publish benchmark did not run any iterations.");

        return new ArchiveReplayPublishBenchmarkResult(
            measurement.FilePath,
            measurement.Decompressor,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            measurement.FileSizeBytes,
            measurement.CompressedRecordCount,
            measurement.CompressedBytes,
            measurement.DecompressedBytes,
            measurement.PublishedEvents,
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
            stopwatch.Elapsed,
            allocatedBytes);
    }

    private static bool HasSameTotals(
        ArchiveReplayPublishResult expected,
        ArchiveReplayPublishResult actual) =>
        expected.CompressedRecordCount == actual.CompressedRecordCount &&
        expected.CompressedBytes == actual.CompressedBytes &&
        expected.DecompressedBytes == actual.DecompressedBytes &&
        expected.PublishedEvents == actual.PublishedEvents &&
        expected.ValidEvents == actual.ValidEvents &&
        expected.BelowThresholdEvents == actual.BelowThresholdEvents &&
        expected.RangeFoldedEvents == actual.RangeFoldedEvents &&
        expected.ClutterFilterNotAppliedEvents == actual.ClutterFilterNotAppliedEvents &&
        expected.PointClutterFilterAppliedEvents == actual.PointClutterFilterAppliedEvents &&
        expected.DualPolarizationFilteredEvents == actual.DualPolarizationFilteredEvents &&
        expected.ReservedEvents == actual.ReservedEvents &&
        expected.UnsupportedEvents == actual.UnsupportedEvents &&
        expected.RawValueChecksum == actual.RawValueChecksum &&
        expected.CalibratedValueScaledChecksum == actual.CalibratedValueScaledChecksum &&
        expected.ChronologyChecksum == actual.ChronologyChecksum;
}
