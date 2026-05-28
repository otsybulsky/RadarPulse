using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayShapeBenchmark
{
    private static IReadOnlyList<ArchiveTwoReplayShapeBenchmarkWorker> CreateWorkers(
        IArchiveBZip2Decompressor decompressor,
        int degreeOfParallelism,
        string radarId,
        DateTimeOffset volumeTimestamp)
    {
        var workers = new ArchiveTwoReplayShapeBenchmarkWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveTwoReplayShapeBenchmarkWorker(
                decompressor.CreateSession(),
                radarId,
                volumeTimestamp);
        }

        return workers;
    }

    private static ArchiveTwoReplayShapeIterationMeasurement MeasureIteration(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveTwoReplayShapeBenchmarkWorker> workers,
        CancellationToken cancellationToken) =>
        degreeOfParallelism == 1
            ? MeasureIterationSequential(fileInfo, workers[0], cancellationToken)
            : MeasureIterationParallel(fileInfo, degreeOfParallelism, workers, cancellationToken);

    private static ArchiveTwoReplayShapeIterationMeasurement MeasureIterationSequential(
        FileInfo fileInfo,
        ArchiveTwoReplayShapeBenchmarkWorker worker,
        CancellationToken cancellationToken)
    {
        var measurement = new ArchiveTwoReplayShapeIterationMeasurement();
        worker.ResetProjection(measurement.AcceptEvent, default);

        var controlWordBuffer = new byte[4];
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

            measurement.CompressedRecordCount++;
            measurement.CompressedBytes += compressedSizeBytes;
            measurement.DecompressedBytes += worker.ProjectRecordContinuing(
                compressedPayloadBuffer,
                compressedSizeBytes,
                measurement.CompressedRecordCount);
        }

        return measurement;
    }

    private static ArchiveTwoReplayShapeIterationMeasurement MeasureIterationParallel(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveTwoReplayShapeBenchmarkWorker> workers,
        CancellationToken cancellationToken)
    {
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var metadataByRecord = new ArchiveTwoReplayShapeRecordMetadata[records.Count];
        var measurementsByRecord = new ArchiveTwoReplayShapeIterationMeasurement[records.Count];
        var availableWorkers = new ConcurrentStack<ArchiveTwoReplayShapeBenchmarkWorker>(workers);

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

        RunParallelRecordPass(
            records,
            fileHandle,
            availableWorkers,
            options,
            (worker, record, compressedPayloadBuffer) =>
            {
                metadataByRecord[record.Index] = worker.ReadRecordMetadata(
                    compressedPayloadBuffer,
                    record.CompressedSizeBytes,
                    record.Index + 1);
            });

        var startingStatesByRecord = BuildStartingProjectorStates(metadataByRecord);

        RunParallelRecordPass(
            records,
            fileHandle,
            availableWorkers,
            options,
            (worker, record, compressedPayloadBuffer) =>
            {
                var measurement = new ArchiveTwoReplayShapeIterationMeasurement
                {
                    CompressedRecordCount = 1,
                    CompressedBytes = record.CompressedSizeBytes
                };
                worker.ResetProjection(measurement.AcceptEvent, startingStatesByRecord[record.Index]);
                measurement.DecompressedBytes = worker.ProjectRecordContinuing(
                    compressedPayloadBuffer,
                    record.CompressedSizeBytes,
                    record.Index + 1);
                measurementsByRecord[record.Index] = measurement;
            });

        var total = new ArchiveTwoReplayShapeIterationMeasurement();
        for (var i = 0; i < measurementsByRecord.Length; i++)
        {
            total.AddOrdered(measurementsByRecord[i]);
        }

        return total;
    }

}
