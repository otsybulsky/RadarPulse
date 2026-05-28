using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayPublishSession
{
    private sealed class ArchiveReplaySessionWorker : IDisposable
    {
        private readonly IArchiveBZip2DecompressionSession decompressionSession;
        private readonly byte[] outputBuffer;
        private readonly ArchiveReplayRecordMetadataCollector metadataCollector = new();
        private readonly ArchiveTwoMessageStreamScanner metadataScanner;
        private readonly ArchiveTwoGateMomentEventProjector projector;
        private readonly ArchiveTwoMessageStreamScanner projectorScanner;
        private byte[]? compressedPayloadBuffer;
        private bool disposed;

        public ArchiveReplaySessionWorker(IArchiveBZip2DecompressionSession decompressionSession)
        {
            this.decompressionSession = decompressionSession;
            outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
            metadataScanner = new ArchiveTwoMessageStreamScanner(metadataCollector);
            projector = new ArchiveTwoGateMomentEventProjector("INIT", DateTimeOffset.UnixEpoch, AcceptEvent);
            projectorScanner = new ArchiveTwoMessageStreamScanner(projector);
        }

        private Action<ArchiveTwoGateMomentEvent> acceptEvent = _ => { };

        public ArchiveReplayRecordMetadata ReadRecordMetadata(
            byte[] compressedPayloadBuffer,
            int compressedSizeBytes,
            int sourceRecordSequenceNumber)
        {
            metadataCollector.Reset();
            metadataScanner.Reset(sourceRecordSequenceNumber);
            decompressionSession.Decompress(
                compressedPayloadBuffer,
                compressedSizeBytes,
                outputBuffer,
                metadataScanner.Append);
            metadataScanner.Complete();
            return metadataCollector.Build();
        }

        public void ResetProjection(
            string radarId,
            DateTimeOffset volumeTimestamp,
            Action<ArchiveTwoGateMomentEvent> acceptEvent,
            ArchiveTwoGateMomentProjectorState projectorState)
        {
            this.acceptEvent = acceptEvent ?? throw new ArgumentNullException(nameof(acceptEvent));
            projector.Reset(radarId, volumeTimestamp, AcceptEvent, projectorState);
        }

        public long ProjectRecordContinuing(
            byte[] compressedPayloadBuffer,
            int compressedSizeBytes,
            int sourceRecordSequenceNumber)
        {
            projectorScanner.Reset(sourceRecordSequenceNumber);
            var decompressedBytes = decompressionSession.Decompress(
                compressedPayloadBuffer,
                compressedSizeBytes,
                outputBuffer,
                projectorScanner.Append);
            projectorScanner.Complete();
            return decompressedBytes;
        }

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

        private void AcceptEvent(ArchiveTwoGateMomentEvent gateMomentEvent) => acceptEvent(gateMomentEvent);

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }
}
