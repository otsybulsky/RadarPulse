using ICSharpCode.SharpZipLib.BZip2;

namespace RadarPulse.Infrastructure.Archive;

public sealed class SharpZipLibArchiveBZip2Decompressor : IArchiveBZip2Decompressor
{
    public const string DecompressorName = "sharpziplib";

    public string Name => DecompressorName;

    public IArchiveBZip2DecompressionSession CreateSession() => new Session();

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
