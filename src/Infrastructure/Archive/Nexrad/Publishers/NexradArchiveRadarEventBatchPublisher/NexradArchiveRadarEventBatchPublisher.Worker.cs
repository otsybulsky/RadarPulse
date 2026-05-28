using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublisher
{
    private sealed class ArchiveRadarEventBatchWorker : IDisposable
    {
        private readonly IArchiveBZip2DecompressionSession decompressionSession;
        private readonly byte[] outputBuffer;
        private byte[]? compressedPayloadBuffer;
        private byte[] decompressedPayloadBuffer;
        private int decompressedPayloadLength;
        private bool disposed;

        public ArchiveRadarEventBatchWorker(IArchiveBZip2DecompressionSession decompressionSession)
        {
            this.decompressionSession = decompressionSession ?? throw new ArgumentNullException(nameof(decompressionSession));
            outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
            decompressedPayloadBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        }

        public ArchiveRadarEventBatchDecompressedRecord DecompressRecord(
            ArchiveTwoCompressedRecordDescriptor record,
            SafeFileHandle fileHandle)
        {
            var compressedPayloadBuffer = EnsureCompressedPayloadBuffer(record.CompressedSizeBytes);
            ArchiveTwoFileReader.ReadExactly(
                fileHandle,
                compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                record.PayloadOffset);
            ArchiveTwoFileReader.ValidateBZip2Signature(
                compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                record.ControlWordOffset);

            decompressedPayloadLength = 0;
            try
            {
                var decompressedBytes = decompressionSession.Decompress(
                    compressedPayloadBuffer,
                    record.CompressedSizeBytes,
                    outputBuffer,
                    AppendDecompressedChunk);
                if (decompressedBytes != decompressedPayloadLength)
                {
                    throw new InvalidDataException("Decompressed byte count does not match the buffered payload length.");
                }

                return new ArchiveRadarEventBatchDecompressedRecord(
                    this,
                    compressedRecordCount: 1,
                    record.CompressedSizeBytes,
                    decompressedBytes,
                    decompressedPayloadBuffer.AsMemory(0, decompressedPayloadLength));
            }
            catch
            {
                decompressedPayloadLength = 0;
                throw;
            }
        }

        public void CompleteDecompressedRecord() => decompressedPayloadLength = 0;

        private byte[] EnsureCompressedPayloadBuffer(int requiredLength)
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

        private void AppendDecompressedChunk(ReadOnlySpan<byte> chunk)
        {
            EnsureDecompressedPayloadCapacity(checked(decompressedPayloadLength + chunk.Length));
            chunk.CopyTo(decompressedPayloadBuffer.AsSpan(decompressedPayloadLength));
            decompressedPayloadLength += chunk.Length;
        }

        private void EnsureDecompressedPayloadCapacity(int requiredLength)
        {
            if (decompressedPayloadBuffer.Length >= requiredLength)
            {
                return;
            }

            var newLength = decompressedPayloadBuffer.Length;
            while (newLength < requiredLength)
            {
                newLength = checked(newLength * 2);
            }

            var newBuffer = ArrayPool<byte>.Shared.Rent(newLength);
            decompressedPayloadBuffer.AsSpan(0, decompressedPayloadLength).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(decompressedPayloadBuffer);
            decompressedPayloadBuffer = newBuffer;
        }

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

            ArrayPool<byte>.Shared.Return(decompressedPayloadBuffer);
            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }
}
