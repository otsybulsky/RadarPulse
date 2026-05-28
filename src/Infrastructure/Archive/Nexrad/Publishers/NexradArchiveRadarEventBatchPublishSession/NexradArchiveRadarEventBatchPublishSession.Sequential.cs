using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublishSession
{
    private ArchiveRadarEventBatchPublishResult PublishFileSequential(
        FileInfo fileInfo,
        IArchiveRadarEventBatchPublisher publisher,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var countingPublisher = publisher as ArchiveRadarEventBatchCountingPublisher ??
            new ArchiveRadarEventBatchCountingPublisher(publisher);
        var initialPayloadCapacity = EstimateInitialPayloadCapacity(fileInfo);
        var projector = PrepareProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            EstimateInitialEventCapacity(
                compressedRecordCount: null,
                options.SourceUniverse.RangeBandCount,
                initialPayloadCapacity),
            initialPayloadCapacity);
        var scanner = this.scanner ?? throw new InvalidOperationException("Archive stream scanner was not initialized.");
        var worker = workers[0];
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
            scanner.Reset(compressedRecordCount);
            decompressedBytes += worker.DecompressTo(
                compressedPayloadBuffer,
                compressedSizeBytes,
                scanner.Append);
            scanner.Complete();
        }

        projector.PublishLeasedBatch(countingPublisher, cancellationToken);

        return countingPublisher.BuildResult(
            fileInfo.FullName,
            decompressor.Name,
            options.DegreeOfParallelism,
            fileInfo.Length,
            compressedRecordCount,
            compressedBytes,
            decompressedBytes,
            projector.DictionarySnapshot);
    }
}
