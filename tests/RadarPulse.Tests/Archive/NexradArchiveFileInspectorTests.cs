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
            Assert.Empty(inspection.CompressedRecords);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task ArchiveTwoCompressedRecordsParseControlWordsAndBZip2Signature()
    {
        var firstPayload = ValidBZip2MetadataPayload();
        var contents = BuildArchiveTwoHeader()
            .Concat(BuildCompressedRecord(firstPayload.Length, firstPayload))
            .Concat(BuildCompressedRecord(-4, [0x10, 0x11, 0x12, 0x13]))
            .ToArray();
        var path = WriteTempFile("KTLX20260504_000245_V06", contents);
        try
        {
            var inspection = await new NexradArchiveFileInspector().InspectAsync(path, CancellationToken.None);

            Assert.Equal(NexradArchiveFileKind.ArchiveTwoBaseData, inspection.FileKind);
            Assert.Null(inspection.Diagnostic);
            Assert.Collection(
                inspection.CompressedRecords,
                first =>
                {
                    Assert.Equal(1, first.SequenceNumber);
                    Assert.Equal(24, first.ControlWordOffset);
                    Assert.Equal(firstPayload.Length, first.ControlWord);
                    Assert.Equal(firstPayload.Length, first.CompressedSizeBytes);
                    Assert.True(first.StartsWithBZip2Signature);
                    Assert.Equal(8, first.DecompressedSizeBytes);
                    Assert.Null(first.DecompressionDiagnostic);
                },
                second =>
                {
                    Assert.Equal(2, second.SequenceNumber);
                    Assert.Equal(28 + firstPayload.Length, second.ControlWordOffset);
                    Assert.Equal(-4, second.ControlWord);
                    Assert.Equal(4, second.CompressedSizeBytes);
                    Assert.False(second.StartsWithBZip2Signature);
                    Assert.Null(second.DecompressedSizeBytes);
                    Assert.Contains("does not start", second.DecompressionDiagnostic);
                });
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task ArchiveTwoCompressedRecordReportsInvalidBZip2Payload()
    {
        var contents = BuildArchiveTwoHeader()
            .Concat(BuildCompressedRecord(6, [(byte)'B', (byte)'Z', (byte)'h', (byte)'9', 0x01, 0x02]))
            .ToArray();
        var path = WriteTempFile("KTLX20260504_000245_V06", contents);
        try
        {
            var inspection = await new NexradArchiveFileInspector().InspectAsync(path, CancellationToken.None);

            var record = Assert.Single(inspection.CompressedRecords);
            Assert.True(record.StartsWithBZip2Signature);
            Assert.Null(record.DecompressedSizeBytes);
            Assert.NotNull(record.DecompressionDiagnostic);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task ArchiveTwoCompressedRecordReportsDeclaredSizePastEnd()
    {
        var contents = BuildArchiveTwoHeader()
            .Concat(BuildCompressedRecordControlWord(10))
            .Concat(new byte[] { (byte)'B', (byte)'Z', (byte)'h' })
            .ToArray();
        var path = WriteTempFile("KTLX20260504_000245_V06", contents);
        try
        {
            var inspection = await new NexradArchiveFileInspector().InspectAsync(path, CancellationToken.None);

            Assert.Equal(NexradArchiveFileKind.ArchiveTwoBaseData, inspection.FileKind);
            Assert.Empty(inspection.CompressedRecords);
            Assert.Contains("declares 10 bytes", inspection.Diagnostic);
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

    private static byte[] BuildCompressedRecord(int controlWord, byte[] compressedPayload) =>
        BuildCompressedRecordControlWord(controlWord).Concat(compressedPayload).ToArray();

    private static byte[] BuildCompressedRecordControlWord(int controlWord)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, controlWord);
        return buffer;
    }

    private static byte[] ValidBZip2MetadataPayload() =>
    [
        0x42, 0x5A, 0x68, 0x39, 0x31, 0x41, 0x59, 0x26,
        0x53, 0x59, 0x01, 0xFE, 0xBF, 0xBC, 0x00, 0x00,
        0x02, 0x81, 0x80, 0x26, 0x02, 0x04, 0x00, 0x20,
        0x00, 0x30, 0xCD, 0x00, 0xC1, 0xA0, 0xAD, 0x21,
        0x38, 0xBB, 0x92, 0x29, 0xC2, 0x84, 0x80, 0x0F,
        0xF5, 0xFD, 0xE0
    ];

    private static string WriteTempFile(string fileName, byte[] contents)
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }
}


