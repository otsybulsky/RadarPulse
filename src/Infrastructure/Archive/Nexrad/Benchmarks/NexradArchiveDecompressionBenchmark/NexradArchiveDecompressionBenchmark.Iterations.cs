using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveDecompressionBenchmark
{
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
}
