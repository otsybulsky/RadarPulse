using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveReplayPublisherTests
{
    private sealed class FakeArchiveBZip2Decompressor : IArchiveBZip2Decompressor
    {
        private readonly IReadOnlyDictionary<byte, byte[]> decompressedRecords;

        private readonly IReadOnlyDictionary<byte, int> delayMillisecondsByRecord;

        public FakeArchiveBZip2Decompressor(
            IReadOnlyDictionary<byte, byte[]> decompressedRecords,
            IReadOnlyDictionary<byte, int>? delayMillisecondsByRecord = null)
        {
            this.decompressedRecords = decompressedRecords;
            this.delayMillisecondsByRecord = delayMillisecondsByRecord ?? new Dictionary<byte, int>();
        }

        public string Name => "fake";

        public IArchiveBZip2DecompressionSession CreateSession() =>
            new Session(decompressedRecords, delayMillisecondsByRecord);

        public long Decompress(
            byte[] compressedPayload,
            int compressedSizeBytes,
            byte[] outputBuffer,
            ArchiveBZip2DecompressedChunkHandler? chunkHandler) =>
            CreateSession().Decompress(compressedPayload, compressedSizeBytes, outputBuffer, chunkHandler);

        public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
            CreateSession().CountDecompressedBytes(compressedPayload, compressedSizeBytes, outputBuffer);

        private sealed class Session : IArchiveBZip2DecompressionSession
        {
            private readonly IReadOnlyDictionary<byte, byte[]> decompressedRecords;
            private readonly IReadOnlyDictionary<byte, int> delayMillisecondsByRecord;

            public Session(
                IReadOnlyDictionary<byte, byte[]> decompressedRecords,
                IReadOnlyDictionary<byte, int> delayMillisecondsByRecord)
            {
                this.decompressedRecords = decompressedRecords;
                this.delayMillisecondsByRecord = delayMillisecondsByRecord;
            }

            public long Decompress(
                byte[] compressedPayload,
                int compressedSizeBytes,
                byte[] outputBuffer,
                ArchiveBZip2DecompressedChunkHandler? chunkHandler)
            {
                var record = ReadRecord(compressedPayload, compressedSizeBytes);
                if (chunkHandler is null)
                {
                    return record.Length;
                }

                var firstChunkLength = Math.Min(5, record.Length);
                chunkHandler(record.AsSpan(0, firstChunkLength));
                if (firstChunkLength < record.Length)
                {
                    chunkHandler(record.AsSpan(firstChunkLength));
                }

                return record.Length;
            }

            public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
                ReadRecord(compressedPayload, compressedSizeBytes).Length;

            private byte[] ReadRecord(byte[] compressedPayload, int compressedSizeBytes)
            {
                if (compressedSizeBytes < 4 ||
                    compressedPayload[0] != (byte)'B' ||
                    compressedPayload[1] != (byte)'Z' ||
                    compressedPayload[2] != (byte)'h')
                {
                    throw new InvalidDataException("Fake compressed payload does not start with BZh.");
                }

                var recordKey = compressedPayload[3];
                if (delayMillisecondsByRecord.TryGetValue(recordKey, out var delayMilliseconds) &&
                    delayMilliseconds > 0)
                {
                    Thread.Sleep(delayMilliseconds);
                }

                return decompressedRecords[recordKey];
            }
        }
    }
}
