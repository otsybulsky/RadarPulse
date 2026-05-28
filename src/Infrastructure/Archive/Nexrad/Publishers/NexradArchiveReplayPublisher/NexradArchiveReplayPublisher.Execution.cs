using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayPublisher
{
    private ArchiveReplayPublishResult PublishFileSequential(
        FileInfo fileInfo,
        IArchiveReplayEventPublisher publisher,
        ArchiveReplayPublishOptions options,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var countingPublisher = publisher as ArchiveReplayCountingPublisher ?? new ArchiveReplayCountingPublisher(publisher);
        var projector = new ArchiveTwoGateMomentEventProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            gateMomentEvent => countingPublisher.Publish(gateMomentEvent, cancellationToken));
        var scanner = new ArchiveTwoMessageStreamScanner(projector);
        var decompressionSession = decompressor.CreateSession();
        var outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        byte[]? compressedPayloadBuffer = null;
        var controlWordBuffer = new byte[4];
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;

        try
        {
            using var stream = File.OpenRead(fileInfo.FullName);
            stream.Position = ArchiveTwoFileReader.VolumeHeaderLength;

            while (stream.Position < stream.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var controlWordOffset = stream.Position;
                var compressedSizeBytes = ArchiveTwoFileReader.ReadCompressedRecordSize(
                    stream,
                    controlWordBuffer,
                    controlWordOffset);
                compressedPayloadBuffer = ArchiveTwoFileReader.EnsurePooledBufferCapacity(
                    compressedPayloadBuffer,
                    compressedSizeBytes);
                ArchiveTwoFileReader.ReadExactly(stream, compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));
                ArchiveTwoFileReader.ValidateBZip2Signature(
                    compressedPayloadBuffer.AsSpan(0, compressedSizeBytes),
                    controlWordOffset);

                compressedRecordCount++;
                compressedBytes += compressedSizeBytes;
                scanner.Reset(compressedRecordCount);
                decompressedBytes += decompressionSession.Decompress(
                    compressedPayloadBuffer,
                    compressedSizeBytes,
                    outputBuffer,
                    scanner.Append);
                scanner.Complete();
            }

            return countingPublisher.BuildResult(
                fileInfo.FullName,
                decompressor.Name,
                options.DegreeOfParallelism,
                fileInfo.Length,
                compressedRecordCount,
                compressedBytes,
                decompressedBytes);
        }
        finally
        {
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }

    private ArchiveReplayPublishResult PublishFileParallelCounting(
        FileInfo fileInfo,
        ArchiveReplayPublishOptions options,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var metadataByRecord = new ArchiveReplayRecordMetadata[records.Count];
        var measurementsByRecord = new ArchiveReplayRecordMeasurement[records.Count];
        var workers = CreateWorkers(options.DegreeOfParallelism, volumeHeader.RadarId, volumeHeader.VolumeTimestamp);

        try
        {
            using var fileHandle = File.OpenHandle(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileOptions.RandomAccess);
            var availableWorkers = new ConcurrentStack<ArchiveReplayWorker>(workers);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = options.DegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            RunParallelRecordPass(
                records,
                fileHandle,
                availableWorkers,
                parallelOptions,
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
                parallelOptions,
                (worker, record, compressedPayloadBuffer) =>
                {
                    var accumulator = new ArchiveReplayEventAccumulator();
                    worker.ResetProjection(accumulator.AcceptEvent, startingStatesByRecord[record.Index]);
                    var decompressedBytes = worker.ProjectRecordContinuing(
                        compressedPayloadBuffer,
                        record.CompressedSizeBytes,
                        record.Index + 1);
                    measurementsByRecord[record.Index] = new ArchiveReplayRecordMeasurement(
                        CompressedRecordCount: 1,
                        record.CompressedSizeBytes,
                        decompressedBytes,
                        accumulator);
                });

            return BuildOrderedResult(
                fileInfo,
                options.DegreeOfParallelism,
                measurementsByRecord);
        }
        finally
        {
            foreach (var worker in workers)
            {
                worker.Dispose();
            }
        }
    }

    private ArchiveReplayPublishResult PublishFileParallelBuffered(
        FileInfo fileInfo,
        IArchiveReplayEventPublisher publisher,
        ArchiveReplayPublishOptions options,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var metadataByRecord = new ArchiveReplayRecordMetadata[records.Count];
        var workers = CreateWorkers(options.DegreeOfParallelism, volumeHeader.RadarId, volumeHeader.VolumeTimestamp);
        var countingPublisher = publisher as ArchiveReplayCountingPublisher ?? new ArchiveReplayCountingPublisher(publisher);
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;
        var inFlight = new Dictionary<int, Task<ArchiveReplayBufferedRecord>>();

        try
        {
            using var fileHandle = File.OpenHandle(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileOptions.RandomAccess);
            var availableWorkers = new ConcurrentStack<ArchiveReplayWorker>(workers);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = options.DegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            RunParallelRecordPass(
                records,
                fileHandle,
                availableWorkers,
                parallelOptions,
                (worker, record, compressedPayloadBuffer) =>
                {
                    metadataByRecord[record.Index] = worker.ReadRecordMetadata(
                        compressedPayloadBuffer,
                        record.CompressedSizeBytes,
                        record.Index + 1);
                });

            var startingStatesByRecord = BuildStartingProjectorStates(metadataByRecord);
            var nextRecordToSchedule = 0;
            while (nextRecordToSchedule < records.Count &&
                inFlight.Count < options.DegreeOfParallelism)
            {
                ScheduleBufferedRecord(
                    records,
                    startingStatesByRecord,
                    fileHandle,
                    availableWorkers,
                    inFlight,
                    nextRecordToSchedule,
                    cancellationToken);
                nextRecordToSchedule++;
            }

            for (var recordIndex = 0; recordIndex < records.Count; recordIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var recordResult = inFlight[recordIndex].GetAwaiter().GetResult();
                inFlight.Remove(recordIndex);

                if (nextRecordToSchedule < records.Count)
                {
                    ScheduleBufferedRecord(
                        records,
                        startingStatesByRecord,
                        fileHandle,
                        availableWorkers,
                        inFlight,
                        nextRecordToSchedule,
                        cancellationToken);
                    nextRecordToSchedule++;
                }

                compressedRecordCount += recordResult.CompressedRecordCount;
                compressedBytes += recordResult.CompressedBytes;
                decompressedBytes += recordResult.DecompressedBytes;
                foreach (var gateMomentEvent in recordResult.Events)
                {
                    countingPublisher.Publish(gateMomentEvent, cancellationToken);
                }
            }

            return countingPublisher.BuildResult(
                fileInfo.FullName,
                decompressor.Name,
                options.DegreeOfParallelism,
                fileInfo.Length,
                compressedRecordCount,
                compressedBytes,
                decompressedBytes);
        }
        finally
        {
            WaitForInFlightTasks(inFlight.Values);
            foreach (var worker in workers)
            {
                worker.Dispose();
            }
        }
    }

}
