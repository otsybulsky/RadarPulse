using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveReplayPublisherTests
{
    [Fact]
    public void PublishFileRejectsInvalidParallelism()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new NexradArchiveReplayPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>()))
                .PublishFile(
                    "archive",
                    new ArchiveReplayPublishOptions(0),
                    CancellationToken.None));

        Assert.Contains("Degree of parallelism", exception.Message);
    }


    [Fact]
    public void PublishFileRejectsNonArchiveTwoFile()
    {
        var path = WriteTempFile("unknown", [0x01, 0x02, 0x03, 0x04]);
        try
        {
            var exception = Assert.Throws<InvalidDataException>(
                () => new NexradArchiveReplayPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>()))
                    .PublishFile(path, ArchiveReplayPublishOptions.Sequential, CancellationToken.None));

            Assert.Contains("Archive Two volume header", exception.Message);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void PublishFileHonorsCancellation()
    {
        var compressedPayload = BuildFakeBZip2Payload(1);
        var path = WriteTempFile(
            "KTLX20260504_000245_V06",
            BuildArchiveTwoHeader()
                .Concat(BuildCompressedRecord(compressedPayload.Length, compressedPayload))
                .ToArray());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            Assert.Throws<OperationCanceledException>(
                () => new NexradArchiveReplayPublisher(new FakeArchiveBZip2Decompressor(new Dictionary<byte, byte[]>
                    {
                        [1] = BuildMessage(31, BuildEightBitType31Payload("REF", [66]))
                    }))
                    .PublishFile(path, ArchiveReplayPublishOptions.Sequential, cancellation.Token));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

}
