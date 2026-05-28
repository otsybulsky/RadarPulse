using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Measures ordered processing runtime over NEXRAD archive files or cache selections.
/// </summary>
public sealed partial class RadarProcessingArchiveOrderedProcessingBenchmark
{
    private const int MaxAutoSizedCacheRadarOrdinalCount = 256;

    private readonly IArchiveBZip2Decompressor decompressor;

    /// <summary>
    /// Creates a benchmark with the default archive decompressor.
    /// </summary>
    public RadarProcessingArchiveOrderedProcessingBenchmark()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    /// <summary>
    /// Creates a benchmark with an explicit archive decompressor.
    /// </summary>
    public RadarProcessingArchiveOrderedProcessingBenchmark(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    /// <summary>
    /// Measures ordered processing over one local archive file.
    /// </summary>
    public RadarProcessingArchiveOrderedProcessingBenchmarkResult MeasureFile(
        string filePath,
        int iterations,
        int warmupIterations,
        int partitionCount,
        int shardCount,
        int degreeOfParallelism,
        int activeBatchCapacity,
        CancellationToken cancellationToken = default,
        RadarProcessingBenchmarkHandlerSet handlerSet = RadarProcessingBenchmarkHandlerSet.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        RadarProcessingBenchmarkHandlers.EnsureKnown(handlerSet);
        ValidateCommon(
            iterations,
            warmupIterations,
            partitionCount,
            shardCount,
            degreeOfParallelism,
            activeBatchCapacity);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        using var archiveSession = new NexradArchiveRadarEventBatchPublishSession(
            decompressor,
            new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism));

        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunFileIteration(
                archiveSession,
                fileInfo,
                sourceUniverse,
                partitionCount,
                shardCount,
                activeBatchCapacity,
                handlerSet,
                cancellationToken);
        }

        var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        OrderedProcessingIterationTelemetry? expectedIteration = null;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationTelemetry = RunFileIteration(
                archiveSession,
                fileInfo,
                sourceUniverse,
                partitionCount,
                shardCount,
                activeBatchCapacity,
                handlerSet,
                cancellationToken);
            if (expectedIteration.HasValue &&
                !expectedIteration.Value.HasSameStableTotals(iterationTelemetry))
            {
                throw new InvalidDataException("Archive ordered processing benchmark produced inconsistent iteration totals.");
            }

            expectedIteration ??= iterationTelemetry;
        }

        stopwatch.Stop();
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore);
        var measurement = expectedIteration ??
                          throw new InvalidOperationException("Archive ordered processing benchmark did not run.");
        return CreateResult(
            measurement,
            fileInfo.FullName,
            cachePath: null,
            date: null,
            radarId: null,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            sourceUniverse.SourceCount,
            partitionCount,
            shardCount,
            activeBatchCapacity,
            handlerSet,
            stopwatch.Elapsed,
            ExcludeStartupPrewarmAllocation(allocatedBytes, measurement.RetainedPayloadPrewarm, iterations));
    }

    /// <summary>
    /// Measures ordered processing over a bounded cache selection.
    /// </summary>
    public RadarProcessingArchiveOrderedProcessingBenchmarkResult MeasureCache(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        int iterations,
        int warmupIterations,
        int partitionCount,
        int shardCount,
        int degreeOfParallelism,
        int activeBatchCapacity,
        CancellationToken cancellationToken = default,
        RadarProcessingBenchmarkHandlerSet handlerSet = RadarProcessingBenchmarkHandlerSet.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFiles);
        RadarProcessingBenchmarkHandlers.EnsureKnown(handlerSet);
        ValidateCommon(
            iterations,
            warmupIterations,
            partitionCount,
            shardCount,
            degreeOfParallelism,
            activeBatchCapacity);

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var sourceUniverse = CreateCacheSourceUniverse(
            directoryInfo,
            date,
            normalizedRadarId,
            maxFiles,
            cancellationToken);
        using var archiveSession = new NexradArchiveRadarEventBatchPublishSession(
            decompressor,
            new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism));

        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunCacheIteration(
                archiveSession,
                directoryInfo,
                date,
                normalizedRadarId,
                maxFiles,
                sourceUniverse,
                partitionCount,
                shardCount,
                activeBatchCapacity,
                handlerSet,
                cancellationToken);
        }

        var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        OrderedProcessingIterationTelemetry? expectedIteration = null;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationTelemetry = RunCacheIteration(
                archiveSession,
                directoryInfo,
                date,
                normalizedRadarId,
                maxFiles,
                sourceUniverse,
                partitionCount,
                shardCount,
                activeBatchCapacity,
                handlerSet,
                cancellationToken);
            if (expectedIteration.HasValue &&
                !expectedIteration.Value.HasSameStableTotals(iterationTelemetry))
            {
                throw new InvalidDataException("Archive ordered processing cache benchmark produced inconsistent iteration totals.");
            }

            expectedIteration ??= iterationTelemetry;
        }

        stopwatch.Stop();
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore);
        var measurement = expectedIteration ??
                          throw new InvalidOperationException("Archive ordered processing cache benchmark did not run.");
        return CreateResult(
            measurement,
            filePath: null,
            directoryInfo.FullName,
            date,
            normalizedRadarId,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            sourceUniverse.SourceCount,
            partitionCount,
            shardCount,
            activeBatchCapacity,
            handlerSet,
            stopwatch.Elapsed,
            ExcludeStartupPrewarmAllocation(allocatedBytes, measurement.RetainedPayloadPrewarm, iterations));
    }

    private static long ExcludeStartupPrewarmAllocation(
        long allocatedBytes,
        RadarProcessingRetainedPayloadPrewarmResult prewarm,
        int iterations)
    {
        if (!prewarm.Applied)
        {
            return allocatedBytes;
        }

        var prewarmAllocatedBytes = checked(prewarm.AllocatedBytes * iterations);
        return allocatedBytes > prewarmAllocatedBytes
            ? allocatedBytes - prewarmAllocatedBytes
            : 0;
    }
}
