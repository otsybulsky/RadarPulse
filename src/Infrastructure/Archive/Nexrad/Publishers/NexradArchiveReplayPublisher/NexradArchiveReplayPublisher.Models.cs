using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayPublisher
{
    private sealed record ArchiveReplayRecordMetadata(
        IReadOnlyList<ArchiveReplayRadialMetadata> Radials);

    private readonly record struct ArchiveReplayRadialMetadata(
        int RadialStatus,
        int ElevationNumber);

    private sealed record ArchiveReplayRecordMeasurement(
        int CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes,
        ArchiveReplayEventAccumulator Accumulator);

    private sealed record ArchiveReplayBufferedRecord(
        int CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes,
        IReadOnlyList<ArchiveTwoGateMomentEvent> Events);

    private sealed class ArchiveReplayRecordMetadataCollector : IArchiveTwoMessageConsumer
    {
        private const int MessageHeaderLength = 16;
        private const int Type31DataHeaderMinimumLength = 72;
        private readonly List<ArchiveReplayRadialMetadata> radials = new();

        public void Reset() => radials.Clear();

        public ArchiveReplayRecordMetadata Build() => new(radials.ToArray());

        public void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source)
        {
            if (message.Length < MessageHeaderLength || message[3] != 31)
            {
                return;
            }

            var payload = message[MessageHeaderLength..];
            if (payload.Length < Type31DataHeaderMinimumLength)
            {
                return;
            }

            radials.Add(new ArchiveReplayRadialMetadata(
                payload[21],
                payload[22]));
        }
    }

    private sealed class ArchiveReplayWorker : IDisposable
    {
        private readonly IArchiveBZip2DecompressionSession decompressionSession;
        private readonly byte[] outputBuffer;
        private readonly ArchiveReplayRecordMetadataCollector metadataCollector = new();
        private readonly ArchiveTwoMessageStreamScanner metadataScanner;
        private readonly ArchiveTwoGateMomentEventProjector projector;
        private readonly ArchiveTwoMessageStreamScanner projectorScanner;
        private byte[]? compressedPayloadBuffer;

        public ArchiveReplayWorker(
            IArchiveBZip2DecompressionSession decompressionSession,
            string radarId,
            DateTimeOffset volumeTimestamp)
        {
            this.decompressionSession = decompressionSession;
            outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
            metadataScanner = new ArchiveTwoMessageStreamScanner(metadataCollector);
            projector = new ArchiveTwoGateMomentEventProjector(radarId, volumeTimestamp, AcceptEvent);
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
            Action<ArchiveTwoGateMomentEvent> acceptEvent,
            ArchiveTwoGateMomentProjectorState projectorState)
        {
            this.acceptEvent = acceptEvent;
            projector.Reset(projectorState);
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
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }
}
