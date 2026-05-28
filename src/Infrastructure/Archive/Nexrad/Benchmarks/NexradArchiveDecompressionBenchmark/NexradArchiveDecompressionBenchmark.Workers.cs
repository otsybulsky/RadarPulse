using System.Buffers;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveDecompressionBenchmark
{
    private const int OutputBufferSize = 81920;

    private static IReadOnlyList<ArchiveBZip2BenchmarkWorker> CreateWorkers(
        IArchiveBZip2Decompressor decompressor,
        int degreeOfParallelism)
    {
        var workers = new ArchiveBZip2BenchmarkWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveBZip2BenchmarkWorker(decompressor.CreateSession());
        }

        return workers;
    }

    private sealed class ArchiveBZip2BenchmarkWorker : IDisposable
    {
        private byte[]? compressedPayloadBuffer;

        public ArchiveBZip2BenchmarkWorker(IArchiveBZip2DecompressionSession decompressionSession)
        {
            DecompressionSession = decompressionSession;
            OutputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        }

        public IArchiveBZip2DecompressionSession DecompressionSession { get; }

        public byte[] OutputBuffer { get; }

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

        public void Dispose()
        {
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(OutputBuffer);
        }
    }
}
