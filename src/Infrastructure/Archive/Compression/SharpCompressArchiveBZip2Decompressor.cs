using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Archive BZip2 decompressor adapter backed by SharpCompress.
/// </summary>
public sealed class SharpCompressArchiveBZip2Decompressor : IArchiveBZip2Decompressor
{
    /// <summary>
    /// Stable name for the SharpCompress decompressor adapter.
    /// </summary>
    public const string DecompressorName = "sharpcompress";

    /// <inheritdoc />
    public string Name => DecompressorName;

    /// <inheritdoc />
    public IArchiveBZip2DecompressionSession CreateSession() => new Session();

    /// <inheritdoc />
    public long Decompress(
        byte[] compressedPayload,
        int compressedSizeBytes,
        byte[] outputBuffer,
        ArchiveBZip2DecompressedChunkHandler? chunkHandler) =>
        CreateSession().Decompress(compressedPayload, compressedSizeBytes, outputBuffer, chunkHandler);

    /// <inheritdoc />
    public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
        CreateSession().CountDecompressedBytes(compressedPayload, compressedSizeBytes, outputBuffer);

    private sealed class Session : IArchiveBZip2DecompressionSession
    {
        public long Decompress(
            byte[] compressedPayload,
            int compressedSizeBytes,
            byte[] outputBuffer,
            ArchiveBZip2DecompressedChunkHandler? chunkHandler)
        {
            using var compressedStream = new MemoryStream(
                compressedPayload,
                index: 0,
                count: compressedSizeBytes,
                writable: false,
                publiclyVisible: true);
            using var decompressedStream = BZip2Stream.Create(
                compressedStream,
                CompressionMode.Decompress,
                decompressConcatenated: false,
                leaveOpen: false);

            long decompressedBytes = 0;
            int bytesRead;
            while ((bytesRead = decompressedStream.Read(outputBuffer, 0, outputBuffer.Length)) > 0)
            {
                decompressedBytes += bytesRead;
                chunkHandler?.Invoke(outputBuffer.AsSpan(0, bytesRead));
            }

            return decompressedBytes;
        }

        public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
            Decompress(compressedPayload, compressedSizeBytes, outputBuffer, null);
    }
}
