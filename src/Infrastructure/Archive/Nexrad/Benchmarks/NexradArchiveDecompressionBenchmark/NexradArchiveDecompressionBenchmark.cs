using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Measures Archive II compressed-record BZip2 decompression throughput and allocation.
/// </summary>
public sealed partial class NexradArchiveDecompressionBenchmark
{
    /// <summary>
    /// Measures decompression with the default decompressor and sequential processing.
    /// </summary>
    public ArchiveTwoDecompressionBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        CancellationToken cancellationToken) =>
        Measure(
            filePath,
            iterations,
            warmupIterations,
            1,
            ArchiveBZip2Decompressors.DefaultName,
            cancellationToken);

    /// <summary>
    /// Measures decompression with the default decompressor and an explicit parallelism degree.
    /// </summary>
    public ArchiveTwoDecompressionBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        CancellationToken cancellationToken) =>
        Measure(
            filePath,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            ArchiveBZip2Decompressors.DefaultName,
            cancellationToken);

    /// <summary>
    /// Measures decompression with an explicit decompressor and parallelism degree.
    /// </summary>
    public ArchiveTwoDecompressionBenchmarkResult Measure(
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

        ArchiveTwoFileReader.ValidateVolumeHeaderSignature(fileInfo);

        var workers = CreateWorkers(decompressor, degreeOfParallelism);
        try
        {
            for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MeasureIteration(fileInfo, degreeOfParallelism, workers, cancellationToken);
            }

            var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            int? compressedRecordsPerIteration = null;
            long? compressedBytesPerIteration = null;
            long? decompressedBytesPerIteration = null;

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var iterationResult = MeasureIteration(fileInfo, degreeOfParallelism, workers, cancellationToken);

                compressedRecordsPerIteration ??= iterationResult.CompressedRecordCount;
                compressedBytesPerIteration ??= iterationResult.CompressedBytes;
                decompressedBytesPerIteration ??= iterationResult.DecompressedBytes;

                if (compressedRecordsPerIteration != iterationResult.CompressedRecordCount ||
                    compressedBytesPerIteration != iterationResult.CompressedBytes ||
                    decompressedBytesPerIteration != iterationResult.DecompressedBytes)
                {
                    throw new InvalidDataException("Archive decompression benchmark produced inconsistent iteration totals.");
                }
            }

            stopwatch.Stop();
            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;

            return new ArchiveTwoDecompressionBenchmarkResult(
                filePath,
                decompressor.Name,
                iterations,
                warmupIterations,
                degreeOfParallelism,
                fileInfo.Length,
                compressedRecordsPerIteration ?? 0,
                compressedBytesPerIteration ?? 0,
                decompressedBytesPerIteration ?? 0,
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
