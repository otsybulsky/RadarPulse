using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayPublishSession
{
    private ArchiveReplayPublishResult PublishFileSequential(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var worker = workers[0];
        var controlWordBuffer = new byte[4];
        totalAccumulator.Reset();
        worker.ResetProjection(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            totalAccumulator.AcceptEvent,
            default);

        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;

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
            var compressedPayloadBuffer = worker.EnsureCompressedPayloadBuffer(compressedSizeBytes);
            ArchiveTwoFileReader.ReadExactly(stream, compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));
            ArchiveTwoFileReader.ValidateBZip2Signature(
                compressedPayloadBuffer.AsSpan(0, compressedSizeBytes),
                controlWordOffset);

            compressedRecordCount++;
            compressedBytes += compressedSizeBytes;
            decompressedBytes += worker.ProjectRecordContinuing(
                compressedPayloadBuffer,
                compressedSizeBytes,
                compressedRecordCount);
        }

        return totalAccumulator.BuildResult(
            fileInfo.FullName,
            decompressor.Name,
            DegreeOfParallelism,
            fileInfo.Length,
            compressedRecordCount,
            compressedBytes,
            decompressedBytes);
    }

    private ArchiveReplayPublishResult PublishFileParallel(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        EnsureRecordCapacity(records.Count);

        using var fileHandle = File.OpenHandle(
            fileInfo.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.RandomAccess);
        var availableWorkers = new ConcurrentStack<ArchiveReplaySessionWorker>(workers);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = DegreeOfParallelism,
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

        BuildStartingProjectorStates(metadataByRecord, startingStatesByRecord, records.Count);

        RunParallelRecordPass(
            records,
            fileHandle,
            availableWorkers,
            parallelOptions,
            (worker, record, compressedPayloadBuffer) =>
            {
                var accumulator = accumulatorsByRecord[record.Index];
                accumulator.Reset();
                worker.ResetProjection(
                    volumeHeader.RadarId,
                    volumeHeader.VolumeTimestamp,
                    accumulator.AcceptEvent,
                    startingStatesByRecord[record.Index]);
                var decompressedBytes = worker.ProjectRecordContinuing(
                    compressedPayloadBuffer,
                    record.CompressedSizeBytes,
                    record.Index + 1);
                compressedRecordCountsByRecord[record.Index] = 1;
                compressedBytesByRecord[record.Index] = record.CompressedSizeBytes;
                decompressedBytesByRecord[record.Index] = decompressedBytes;
            });

        return BuildOrderedResult(fileInfo, records.Count);
    }

}
