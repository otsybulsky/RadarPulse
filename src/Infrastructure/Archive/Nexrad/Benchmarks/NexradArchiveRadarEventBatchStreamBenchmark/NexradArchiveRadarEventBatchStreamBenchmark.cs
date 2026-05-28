using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Measures Archive II projection into radar event batch streams.
/// </summary>
public sealed partial class NexradArchiveRadarEventBatchStreamBenchmark
{
    private readonly IArchiveBZip2Decompressor decompressor;

    /// <summary>
    /// Creates a stream benchmark with the default archive BZip2 decompressor.
    /// </summary>
    public NexradArchiveRadarEventBatchStreamBenchmark()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    /// <summary>
    /// Creates a stream benchmark with an explicit archive BZip2 decompressor.
    /// </summary>
    public NexradArchiveRadarEventBatchStreamBenchmark(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    /// <summary>
    /// Measures one file with an explicit decompressor name.
    /// </summary>
    public ArchiveRadarEventBatchStreamBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        CancellationToken cancellationToken) =>
        new NexradArchiveRadarEventBatchStreamBenchmark(ArchiveBZip2Decompressors.Create(decompressorName))
            .Measure(filePath, iterations, warmupIterations, degreeOfParallelism, cancellationToken);

    /// <summary>
    /// Measures one file using this benchmark instance's decompressor.
    /// </summary>
    public ArchiveRadarEventBatchStreamBenchmarkResult Measure(
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

        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        var options = new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism);
        using var session = new NexradArchiveRadarEventBatchPublishSession(decompressor, options);
        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.PublishFile(fileInfo.FullName, cancellationToken);
        }

        var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ArchiveRadarEventBatchPublishResult? expectedIteration = null;
        RadarStreamDictionarySnapshotMetrics expectedDictionaryMetrics = default;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationResult = session.PublishFile(fileInfo.FullName, cancellationToken);
            var dictionaryMetrics = RadarStreamDictionarySnapshotMetrics.Compute(iterationResult.DictionarySnapshot);
            if (expectedIteration is null)
            {
                expectedIteration = iterationResult;
                expectedDictionaryMetrics = dictionaryMetrics;
            }
            else if (!HasSameTotals(expectedIteration, expectedDictionaryMetrics, iterationResult, dictionaryMetrics))
            {
                throw new InvalidDataException("Radar event batch stream benchmark produced inconsistent iteration totals.");
            }
        }

        stopwatch.Stop();
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
        var measurement = expectedIteration ?? throw new InvalidOperationException("Radar event batch stream benchmark did not run any iterations.");

        return new ArchiveRadarEventBatchStreamBenchmarkResult(
            measurement.FilePath,
            measurement.Decompressor,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            measurement.StreamSchemaVersion,
            measurement.DictionaryVersion,
            measurement.SourceUniverseVersion,
            measurement.FileSizeBytes,
            measurement.CompressedRecordCount,
            measurement.CompressedBytes,
            measurement.DecompressedBytes,
            measurement.BatchCount,
            measurement.EventCount,
            measurement.PayloadBytes,
            measurement.PayloadValueCount,
            measurement.RawValueChecksum,
            expectedDictionaryMetrics.RadarCount,
            expectedDictionaryMetrics.MomentCount,
            expectedDictionaryMetrics.MappingChecksum,
            stopwatch.Elapsed,
            allocatedBytes);
    }

    /// <summary>
    /// Measures a cache selection with an explicit decompressor name.
}
