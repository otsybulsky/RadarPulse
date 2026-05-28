using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublisher
{
    private ArchiveRadarEventBatchPublishResult PublishFileSequential(
        FileInfo fileInfo,
        IArchiveRadarEventBatchPublisher publisher,
        ArchiveRadarEventBatchPublishOptions options,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var countingPublisher = publisher as ArchiveRadarEventBatchCountingPublisher ??
            new ArchiveRadarEventBatchCountingPublisher(publisher);
        var initialPayloadCapacity = EstimateInitialPayloadCapacity(fileInfo);
        var projector = new ArchiveTwoRadarEventBatchProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            options.SourceUniverse,
            EstimateInitialEventCapacity(
                compressedRecordCount: null,
                options.SourceUniverse.RangeBandCount,
                initialPayloadCapacity),
            initialPayloadCapacity);
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

            var batch = projector.BuildBatch();
            if (batch.EventCount > 0)
            {
                countingPublisher.Publish(batch, cancellationToken);
            }

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
        finally
        {
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }
}
