using ICSharpCode.SharpZipLib.BZip2;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Archive BZip2 decompressor adapter backed by SharpZipLib.
/// </summary>
public sealed class SharpZipLibArchiveBZip2Decompressor : IArchiveBZip2Decompressor
{
    /// <summary>
    /// Stable name for the SharpZipLib decompressor adapter.
    /// </summary>
    public const string DecompressorName = "sharpziplib";

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
            using var decompressedStream = new BZip2InputStream(compressedStream);

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
