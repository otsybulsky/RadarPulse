using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Measures Archive II compressed-record BZip2 decompression throughput and allocation.
/// </summary>
public sealed class NexradArchiveDecompressionBenchmark
{
    private const int OutputBufferSize = 81920;

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

    private static ArchiveTwoIterationMeasurement MeasureIteration(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveBZip2BenchmarkWorker> workers,
        CancellationToken cancellationToken) =>
        degreeOfParallelism == 1
            ? MeasureIterationSequential(fileInfo, workers[0], cancellationToken)
            : MeasureIterationParallel(fileInfo, degreeOfParallelism, workers, cancellationToken);

    private static ArchiveTwoIterationMeasurement MeasureIterationSequential(
        FileInfo fileInfo,
        ArchiveBZip2BenchmarkWorker worker,
        CancellationToken cancellationToken)
    {
        var controlWordBuffer = new byte[4];
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;

        using var stream = File.OpenRead(fileInfo.FullName);
        stream.Position = ArchiveTwoFileReader.VolumeHeaderLength;

        while (stream.Position < stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var controlWordOffset = stream.Position;
            var compressedSizeBytes = ArchiveTwoFileReader.ReadCompressedRecordSize(stream, controlWordBuffer, controlWordOffset);

            var compressedPayloadBuffer = worker.EnsureCompressedPayloadBuffer(compressedSizeBytes);
            ArchiveTwoFileReader.ReadExactly(stream, compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));
            ArchiveTwoFileReader.ValidateBZip2Signature(compressedPayloadBuffer.AsSpan(0, compressedSizeBytes), controlWordOffset);

            compressedRecordCount++;
            compressedBytes += compressedSizeBytes;
            decompressedBytes += worker.DecompressionSession.CountDecompressedBytes(
                compressedPayloadBuffer,
                compressedSizeBytes,
                worker.OutputBuffer);
        }

        return new ArchiveTwoIterationMeasurement(compressedRecordCount, compressedBytes, decompressedBytes);
    }

    private static ArchiveTwoIterationMeasurement MeasureIterationParallel(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveBZip2BenchmarkWorker> workers,
        CancellationToken cancellationToken)
    {
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var decompressedBytesByRecord = new long[records.Count];
        var availableWorkers = new ConcurrentStack<ArchiveBZip2BenchmarkWorker>(workers);

        using var fileHandle = File.OpenHandle(
            fileInfo.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.RandomAccess);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = degreeOfParallelism,
            CancellationToken = cancellationToken
        };

        try
        {
            Parallel.For(
                0,
                records.Count,
                options,
                recordIndex =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();
                    var record = records[recordIndex];
                    if (!availableWorkers.TryPop(out var worker))
                    {
                        throw new InvalidOperationException("No BZip2 benchmark worker was available.");
                    }

                    try
                    {
                        var compressedPayloadBuffer = worker.EnsureCompressedPayloadBuffer(record.CompressedSizeBytes);
                        ArchiveTwoFileReader.ReadExactly(
                            fileHandle,
                            compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                            record.PayloadOffset);
                        ArchiveTwoFileReader.ValidateBZip2Signature(
                            compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                            record.ControlWordOffset);

                        decompressedBytesByRecord[record.Index] = worker.DecompressionSession.CountDecompressedBytes(
                            compressedPayloadBuffer,
                            record.CompressedSizeBytes,
                            worker.OutputBuffer);
                    }
                    finally
                    {
                        availableWorkers.Push(worker);
                    }
                });
        }
        catch (AggregateException ex)
        {
            ThrowSingleInnerExceptionWhenUseful(ex);
            throw;
        }

        long compressedBytes = 0;
        long decompressedBytes = 0;
        for (var i = 0; i < records.Count; i++)
        {
            compressedBytes += records[i].CompressedSizeBytes;
            decompressedBytes += decompressedBytesByRecord[i];
        }

        return new ArchiveTwoIterationMeasurement(records.Count, compressedBytes, decompressedBytes);
    }

    private static IReadOnlyList<ArchiveBZip2BenchmarkWorker> CreateWorkers(
        IArchiveBZip2Decompressor decompressor,
        int degreeOfParallelism)
    {
        var workers = new ArchiveBZip2BenchmarkWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveBZip2BenchmarkWorker(decompressor.CreateSession());
        }

        return workers;
    }
    private static void ThrowSingleInnerExceptionWhenUseful(AggregateException exception)
    {
        var flattened = exception.Flatten();
        if (flattened.InnerExceptions.Count != 1)
        {
            return;
        }

        var innerException = flattened.InnerExceptions[0];
        if (innerException is InvalidDataException or IOException)
        {
            ExceptionDispatchInfo.Capture(innerException).Throw();
        }
    }

    private readonly record struct ArchiveTwoIterationMeasurement(
        int CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes);

    private sealed class ArchiveBZip2BenchmarkWorker : IDisposable
    {
        private byte[]? compressedPayloadBuffer;

        public ArchiveBZip2BenchmarkWorker(IArchiveBZip2DecompressionSession decompressionSession)
        {
            DecompressionSession = decompressionSession;
            OutputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        }

        public IArchiveBZip2DecompressionSession DecompressionSession { get; }

        public byte[] OutputBuffer { get; }

        public byte[] EnsureCompressedPayloadBuffer(int requiredLength)
        {
            if (compressedPayloadBuffer is not null && compressedPayloadBuffer.Length >= requiredLength)
            {
                return compressedPayloadBuffer;
            }

            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            compressedPayloadBuffer = ArrayPool<byte>.Shared.Rent(requiredLength);
            return compressedPayloadBuffer;
        }

        public void Dispose()
        {
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(OutputBuffer);
        }
    }
}
