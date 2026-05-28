using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublisher
{
    private sealed class ArchiveRadarEventBatchDecompressedRecord : IDisposable
    {
        private ReadOnlyMemory<byte> decompressedPayload;
        private bool disposed;

        public ArchiveRadarEventBatchDecompressedRecord(
            ArchiveRadarEventBatchWorker worker,
            int compressedRecordCount,
            long compressedBytes,
            long decompressedBytes,
            ReadOnlyMemory<byte> decompressedPayload)
        {
            Worker = worker ?? throw new ArgumentNullException(nameof(worker));
            CompressedRecordCount = compressedRecordCount;
            CompressedBytes = compressedBytes;
            DecompressedBytes = decompressedBytes;
            this.decompressedPayload = decompressedPayload;
        }

        public ArchiveRadarEventBatchWorker Worker { get; }

        public int CompressedRecordCount { get; }

        public long CompressedBytes { get; }

        public long DecompressedBytes { get; }

        public ReadOnlySpan<byte> DecompressedPayload =>
            disposed
                ? throw new ObjectDisposedException(nameof(ArchiveRadarEventBatchDecompressedRecord))
                : decompressedPayload.Span;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            decompressedPayload = default;
            Worker.CompleteDecompressedRecord();
        }
    }
}
