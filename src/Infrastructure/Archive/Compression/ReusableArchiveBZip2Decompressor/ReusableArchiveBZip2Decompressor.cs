using System.Runtime.CompilerServices;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// RadarPulse BZip2 decompressor with reusable scratch buffers for Archive II record payloads.
/// </summary>
/// <remarks>
/// This implementation avoids per-record third-party stream allocation and validates BZip2 block and combined CRCs.
/// </remarks>
public sealed partial class ReusableArchiveBZip2Decompressor : IArchiveBZip2Decompressor
{
    /// <summary>
    /// Stable name for the reusable RadarPulse decompressor.
    /// </summary>
    public const string DecompressorName = "radarpulse";

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
        private readonly BZip2Workspace workspace = new();

        public long Decompress(
            byte[] compressedPayload,
            int compressedSizeBytes,
            byte[] outputBuffer,
            ArchiveBZip2DecompressedChunkHandler? chunkHandler) =>
            workspace.Decompress(compressedPayload, compressedSizeBytes, outputBuffer, chunkHandler);

        public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
            Decompress(compressedPayload, compressedSizeBytes, outputBuffer, null);
    }

}
