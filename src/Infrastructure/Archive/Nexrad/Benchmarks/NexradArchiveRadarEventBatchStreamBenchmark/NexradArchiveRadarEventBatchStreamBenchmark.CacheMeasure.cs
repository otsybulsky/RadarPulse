using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveRadarEventBatchStreamBenchmark
{
    public ArchiveRadarEventBatchStreamCacheBenchmarkResult MeasureCache(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        CancellationToken cancellationToken) =>
        new NexradArchiveRadarEventBatchStreamBenchmark(ArchiveBZip2Decompressors.Create(decompressorName))
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
    public ArchiveRadarEventBatchStreamCacheBenchmarkResult MeasureCache(
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

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        var options = new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism);
        using var session = new NexradArchiveRadarEventBatchPublishSession(decompressor, options);
        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PublishCacheIteration(
                directoryInfo,
                date,
                normalizedRadarId,
                maxFiles,
                session,
                cancellationToken);
        }

        var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        CacheIterationTotals? expectedIteration = null;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationResult = PublishCacheIteration(
                directoryInfo,
                date,
                normalizedRadarId,
                maxFiles,
                session,
                cancellationToken);
            if (expectedIteration is null)
            {
                expectedIteration = iterationResult;
            }
            else if (!expectedIteration.Value.HasSameTotals(iterationResult))
            {
                throw new InvalidDataException("Radar event batch stream cache benchmark produced inconsistent iteration totals.");
            }
        }

        stopwatch.Stop();
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
        var measurement = expectedIteration ?? throw new InvalidOperationException("Radar event batch stream cache benchmark did not run any iterations.");

        return new ArchiveRadarEventBatchStreamCacheBenchmarkResult(
            directoryInfo.FullName,
            date,
            normalizedRadarId,
            decompressor.Name,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            measurement.StreamSchemaVersion,
            measurement.SourceUniverseVersion,
            measurement.ExaminedFiles,
            measurement.SkippedFiles,
            measurement.PublishedFiles,
            measurement.FileSizeBytes,
            measurement.CompressedRecordCount,
            measurement.CompressedBytes,
            measurement.DecompressedBytes,
            measurement.BatchCount,
            measurement.EventCount,
            measurement.PayloadBytes,
            measurement.PayloadValueCount,
            measurement.RawValueChecksum,
            stopwatch.Elapsed,
            allocatedBytes);
    }
}
