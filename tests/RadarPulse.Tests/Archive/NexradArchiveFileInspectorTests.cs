using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed class NexradArchiveFileInspectorTests
{
    [Fact]
    public async Task ArchiveTwoVolumeHeaderParsesMetadata()
    {
        var path = WriteTempFile("KTLX20260504_000245_V06", BuildArchiveTwoHeader());
        try
        {
            var inspection = await new NexradArchiveFileInspector().InspectAsync(path, CancellationToken.None);

            Assert.Equal(NexradArchiveFileKind.ArchiveTwoBaseData, inspection.FileKind);
            Assert.Null(inspection.Diagnostic);
            Assert.NotNull(inspection.ArchiveTwoVolumeHeader);
            Assert.Equal("AR2V0006.266", inspection.ArchiveTwoVolumeHeader.ArchiveFilename);
            Assert.Equal("06", inspection.ArchiveTwoVolumeHeader.Version);
            Assert.Equal(266, inspection.ArchiveTwoVolumeHeader.ExtensionNumber);
            Assert.Equal("KTLX", inspection.ArchiveTwoVolumeHeader.RadarId);
            Assert.Equal(new DateOnly(2026, 5, 4), inspection.ArchiveTwoVolumeHeader.VolumeDate);
            Assert.Equal(TimeSpan.FromMilliseconds(164_018), inspection.ArchiveTwoVolumeHeader.VolumeTime);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task ShortArchiveTwoSignatureReturnsUnknownWithDiagnostic()
    {
        var path = WriteTempFile("short", Encoding.ASCII.GetBytes("AR2V"));
        try
        {
            var inspection = await new NexradArchiveFileInspector().InspectAsync(path, CancellationToken.None);

            Assert.Equal(NexradArchiveFileKind.Unknown, inspection.FileKind);
            Assert.Contains("24-byte volume header", inspection.Diagnostic);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task MdmFileNameIsClassifiedSeparately()
    {
        var path = WriteTempFile("KTLX20260504_005834_V06_MDM", [0x00, 0x0A, 0x57, 0x0F]);
        try
        {
            var inspection = await new NexradArchiveFileInspector().InspectAsync(path, CancellationToken.None);

            Assert.Equal(NexradArchiveFileKind.MdmOrCompressedStream, inspection.FileKind);
            Assert.Null(inspection.ArchiveTwoVolumeHeader);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task EarlyBZip2SignatureWithoutArchiveHeaderIsClassifiedSeparately()
    {
        var path = WriteTempFile("compressed-stream", [0x00, 0x01, (byte)'B', (byte)'Z', (byte)'h', (byte)'9']);
        try
        {
            var inspection = await new NexradArchiveFileInspector().InspectAsync(path, CancellationToken.None);

            Assert.Equal(NexradArchiveFileKind.MdmOrCompressedStream, inspection.FileKind);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task UnknownBinaryReturnsUnknown()
    {
        var path = WriteTempFile("unknown", [0x01, 0x02, 0x03, 0x04]);
        try
        {
            var inspection = await new NexradArchiveFileInspector().InspectAsync(path, CancellationToken.None);

            Assert.Equal(NexradArchiveFileKind.Unknown, inspection.FileKind);
            Assert.Contains("not recognized", inspection.Diagnostic);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    private static byte[] BuildArchiveTwoHeader()
    {
        var header = new byte[24];
        Encoding.ASCII.GetBytes("AR2V0006.266").CopyTo(header, 0);
        BinaryPrimitives.WriteInt32BigEndian(
            header.AsSpan(12, 4),
            new DateOnly(2026, 5, 4).DayNumber - new DateOnly(1970, 1, 1).DayNumber + 1);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), 164_018);
        Encoding.ASCII.GetBytes("KTLX").CopyTo(header, 20);
        return header;
    }

    private static string WriteTempFile(string fileName, byte[] contents)
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }
}


