using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveParseBenchmark
{
    private static ArchiveTwoParseIterationMeasurement MeasureIteration(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveTwoParseBenchmarkWorker> workers,
        CancellationToken cancellationToken) =>
        degreeOfParallelism == 1
            ? MeasureIterationSequential(fileInfo, workers[0], cancellationToken)
            : MeasureIterationParallel(fileInfo, degreeOfParallelism, workers, cancellationToken);

    private static ArchiveTwoParseIterationMeasurement MeasureIterationSequential(
        FileInfo fileInfo,
        ArchiveTwoParseBenchmarkWorker worker,
        CancellationToken cancellationToken)
    {
        var controlWordBuffer = new byte[4];
        var measurement = new ArchiveTwoParseIterationMeasurement();

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

            measurement += worker.MeasureRecord(compressedPayloadBuffer, compressedSizeBytes);
        }

        return measurement;
    }

    private static ArchiveTwoParseIterationMeasurement MeasureIterationParallel(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveTwoParseBenchmarkWorker> workers,
        CancellationToken cancellationToken)
    {
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var measurementsByRecord = new ArchiveTwoParseIterationMeasurement[records.Count];
        var availableWorkers = new ConcurrentStack<ArchiveTwoParseBenchmarkWorker>(workers);

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
                        throw new InvalidOperationException("No archive parse benchmark worker was available.");
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

                        measurementsByRecord[record.Index] = worker.MeasureRecord(
                            compressedPayloadBuffer,
                            record.CompressedSizeBytes);
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

        var total = new ArchiveTwoParseIterationMeasurement();
        for (var i = 0; i < measurementsByRecord.Length; i++)
        {
            total += measurementsByRecord[i];
        }

        return total;
    }
}
