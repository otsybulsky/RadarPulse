using System.Buffers.Binary;
using System.Text;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveDecompressionBenchmarkTests
{
    [Fact]
    public void ReusableDecompressorCountsBZip2PayloadWithLongRuns()
    {
        var original = Enumerable.Repeat((byte)'A', 1_024)
            .Concat(Encoding.ASCII.GetBytes("KTLX"))
            .Concat(Enumerable.Repeat((byte)0, 512))
            .ToArray();
        var compressedPayload = ValidBZip2PayloadWithLongRuns();
        var outputBuffer = new byte[128];

        var reusableBytes = new ReusableArchiveBZip2Decompressor()
            .CreateSession()
            .CountDecompressedBytes(compressedPayload, compressedPayload.Length, outputBuffer);
        var sharpZipLibBytes = new SharpZipLibArchiveBZip2Decompressor()
            .CreateSession()
            .CountDecompressedBytes(compressedPayload, compressedPayload.Length, outputBuffer);

        Assert.Equal(original.Length, reusableBytes);
        Assert.Equal(sharpZipLibBytes, reusableBytes);
    }

    [Fact]
    public void ReusableDecompressorStreamsChunksInOrder()
    {
        var original = Enumerable.Repeat((byte)'A', 1_024)
            .Concat(Encoding.ASCII.GetBytes("KTLX"))
            .Concat(Enumerable.Repeat((byte)0, 512))
            .ToArray();
        var compressedPayload = ValidBZip2PayloadWithLongRuns();
        var outputBuffer = new byte[7];
        using var streamed = new MemoryStream();

        var decompressedBytes = new ReusableArchiveBZip2Decompressor()
            .CreateSession()
            .Decompress(
                compressedPayload,
                compressedPayload.Length,
                outputBuffer,
                chunk => streamed.Write(chunk));

        Assert.Equal(original.Length, decompressedBytes);
        Assert.Equal(original, streamed.ToArray());
    }

    [Fact]
    public void ReusableDecompressorRejectsBZip2CrcMismatch()
    {
        var compressedPayload = ValidBZip2MetadataPayload().ToArray();
        compressedPayload[10] ^= 0x01;
        var outputBuffer = new byte[128];

        var exception = Assert.Throws<InvalidDataException>(
            () => new ReusableArchiveBZip2Decompressor()
                .CreateSession()
                .CountDecompressedBytes(compressedPayload, compressedPayload.Length, outputBuffer));

        Assert.Contains("CRC", exception.Message);
    }
}
