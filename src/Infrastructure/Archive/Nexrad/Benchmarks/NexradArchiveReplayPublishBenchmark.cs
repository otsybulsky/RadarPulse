using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Measures Archive II replay publishing throughput and deterministic totals.
/// </summary>
public sealed class NexradArchiveReplayPublishBenchmark
{
    private readonly IArchiveBZip2Decompressor decompressor;

    /// <summary>
    /// Creates a replay publish benchmark with the default archive BZip2 decompressor.
    /// </summary>
    public NexradArchiveReplayPublishBenchmark()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    /// <summary>
    /// Creates a replay publish benchmark with an explicit archive BZip2 decompressor.
    /// </summary>
    public NexradArchiveReplayPublishBenchmark(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    /// <summary>
    /// Measures one file with an explicit decompressor name.
    /// </summary>
    public ArchiveReplayPublishBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        CancellationToken cancellationToken) =>
        new NexradArchiveReplayPublishBenchmark(ArchiveBZip2Decompressors.Create(decompressorName))
            .Measure(filePath, iterations, warmupIterations, degreeOfParallelism, cancellationToken);

    /// <summary>
    /// Measures one file using this benchmark instance's decompressor.
    /// </summary>
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

    /// <summary>
    /// Measures a cache selection with an explicit decompressor name.
    /// </summary>
    public ArchiveReplayPublishCacheBenchmarkResult MeasureCache(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        CancellationToken cancellationToken) =>
        new NexradArchiveReplayPublishBenchmark(ArchiveBZip2Decompressors.Create(decompressorName))
            .MeasureCache(
                cachePath,
                date,
                radarId,
                maxFiles,
                iterations,
                warmupIterations,
                degreeOfParallelism,
                cancellationToken);

    /// <summary>
    /// Measures a cache selection using this benchmark instance's decompressor.
    /// </summary>
    public ArchiveReplayPublishCacheBenchmarkResult MeasureCache(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        if (maxFiles <= 0)
        {
            throw new ArgumentException("Max files must be greater than zero.", nameof(maxFiles));
        }

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

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        using var session = new NexradArchiveReplayPublishSession(decompressor, degreeOfParallelism);
        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.PublishCache(directoryInfo.FullName, date, radarId, maxFiles, cancellationToken);
        }

        var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ArchiveReplayCachePublishResult? expectedIteration = null;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationResult = session.PublishCache(directoryInfo.FullName, date, radarId, maxFiles, cancellationToken);
            if (expectedIteration is null)
            {
                expectedIteration = iterationResult;
            }
            else if (!HasSameTotals(expectedIteration, iterationResult))
            {
                throw new InvalidDataException("Replay publish cache benchmark produced inconsistent iteration totals.");
            }
        }

        stopwatch.Stop();
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
        var measurement = expectedIteration ?? throw new InvalidOperationException("Replay publish cache benchmark did not run any iterations.");

        return new ArchiveReplayPublishCacheBenchmarkResult(
            measurement.CachePath,
            measurement.Date,
            measurement.RadarId,
            measurement.Decompressor,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            measurement.ExaminedFileCount,
            measurement.SkippedFileCount,
            measurement.PublishedFileCount,
            measurement.TotalFileSizeBytes,
            measurement.TotalCompressedRecordCount,
            measurement.TotalCompressedBytes,
            measurement.TotalDecompressedBytes,
            measurement.TotalPublishedEvents,
            measurement.TotalValidEvents,
            measurement.TotalBelowThresholdEvents,
            measurement.TotalRangeFoldedEvents,
            measurement.TotalClutterFilterNotAppliedEvents,
            measurement.TotalPointClutterFilterAppliedEvents,
            measurement.TotalDualPolarizationFilteredEvents,
            measurement.TotalReservedEvents,
            measurement.TotalUnsupportedEvents,
            measurement.TotalRawValueChecksum,
            measurement.TotalCalibratedValueScaledChecksum,
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

    private static bool HasSameTotals(
        ArchiveReplayCachePublishResult expected,
        ArchiveReplayCachePublishResult actual) =>
        expected.ExaminedFileCount == actual.ExaminedFileCount &&
        expected.SkippedFileCount == actual.SkippedFileCount &&
        expected.PublishedFileCount == actual.PublishedFileCount &&
        expected.TotalFileSizeBytes == actual.TotalFileSizeBytes &&
        expected.TotalCompressedRecordCount == actual.TotalCompressedRecordCount &&
        expected.TotalCompressedBytes == actual.TotalCompressedBytes &&
        expected.TotalDecompressedBytes == actual.TotalDecompressedBytes &&
        expected.TotalPublishedEvents == actual.TotalPublishedEvents &&
        expected.TotalValidEvents == actual.TotalValidEvents &&
        expected.TotalBelowThresholdEvents == actual.TotalBelowThresholdEvents &&
        expected.TotalRangeFoldedEvents == actual.TotalRangeFoldedEvents &&
        expected.TotalClutterFilterNotAppliedEvents == actual.TotalClutterFilterNotAppliedEvents &&
        expected.TotalPointClutterFilterAppliedEvents == actual.TotalPointClutterFilterAppliedEvents &&
        expected.TotalDualPolarizationFilteredEvents == actual.TotalDualPolarizationFilteredEvents &&
        expected.TotalReservedEvents == actual.TotalReservedEvents &&
        expected.TotalUnsupportedEvents == actual.TotalUnsupportedEvents &&
        expected.TotalRawValueChecksum == actual.TotalRawValueChecksum &&
        expected.TotalCalibratedValueScaledChecksum == actual.TotalCalibratedValueScaledChecksum &&
        expected.ChronologyChecksum == actual.ChronologyChecksum;
}
