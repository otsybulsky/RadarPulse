using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveReplayPublisherTests
{
    private static void AssertPublishResultsMatch(
        ArchiveReplayPublishResult expected,
        ArchiveReplayPublishResult actual)
    {
        Assert.Equal(expected.FilePath, actual.FilePath);
        Assert.Equal(expected.Decompressor, actual.Decompressor);
        Assert.Equal(expected.FileSizeBytes, actual.FileSizeBytes);
        Assert.Equal(expected.CompressedRecordCount, actual.CompressedRecordCount);
        Assert.Equal(expected.CompressedBytes, actual.CompressedBytes);
        Assert.Equal(expected.DecompressedBytes, actual.DecompressedBytes);
        Assert.Equal(expected.PublishedEvents, actual.PublishedEvents);
        Assert.Equal(expected.ValidEvents, actual.ValidEvents);
        Assert.Equal(expected.BelowThresholdEvents, actual.BelowThresholdEvents);
        Assert.Equal(expected.RangeFoldedEvents, actual.RangeFoldedEvents);
        Assert.Equal(expected.ClutterFilterNotAppliedEvents, actual.ClutterFilterNotAppliedEvents);
        Assert.Equal(expected.PointClutterFilterAppliedEvents, actual.PointClutterFilterAppliedEvents);
        Assert.Equal(expected.DualPolarizationFilteredEvents, actual.DualPolarizationFilteredEvents);
        Assert.Equal(expected.ReservedEvents, actual.ReservedEvents);
        Assert.Equal(expected.UnsupportedEvents, actual.UnsupportedEvents);
        Assert.Equal(expected.RawValueChecksum, actual.RawValueChecksum);
        Assert.Equal(expected.CalibratedValueScaledChecksum, actual.CalibratedValueScaledChecksum);
        Assert.Equal(expected.ChronologyChecksum, actual.ChronologyChecksum);
    }

    private static string WriteTempFile(string fileName, byte[] contents)
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        return WriteFile(directory, fileName, contents);
    }

    private static string WriteFile(string directory, string fileName, byte[] contents)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }

}
