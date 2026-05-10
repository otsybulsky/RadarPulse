using ICSharpCode.SharpZipLib.BZip2;

namespace RadarPulse.Infrastructure.Archive;

public sealed class SharpZipLibArchiveBZip2Decompressor : IArchiveBZip2Decompressor
{
    public const string DecompressorName = "sharpziplib";

    public string Name => DecompressorName;

    public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer)
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
        }

        return decompressedBytes;
    }
}
