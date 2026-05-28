using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Archive;

public sealed partial class NexradArchiveRadarEventBatchPublisherTests
{
    private sealed class CapturingRadarEventBatchPublisher : IArchiveRadarEventBatchPublisher
    {
        private readonly List<RadarEventBatch> batches = new();

        public IReadOnlyList<RadarEventBatch> Batches => batches;

        public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batches.Add(batch.ToOwnedSnapshot());
        }
    }

    private sealed class LeasedCapturingRadarEventBatchPublisher : IArchiveRadarEventBatchPublisher
    {
        private readonly List<RadarEventBatch> batches = new();

        public IReadOnlyList<RadarEventBatch> Batches => batches;

        public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            batches.Add(batch.ToOwnedSnapshot());
        }
    }

    private static void AssertArchiveRadarEventBatchPublishTotalsEqual(
        ArchiveRadarEventBatchPublishResult expected,
        ArchiveRadarEventBatchPublishResult actual)
    {
        Assert.Equal(expected.FilePath, actual.FilePath);
        Assert.Equal(expected.Decompressor, actual.Decompressor);
        Assert.Equal(expected.DegreeOfParallelism, actual.DegreeOfParallelism);
        Assert.Equal(expected.FileSizeBytes, actual.FileSizeBytes);
        Assert.Equal(expected.CompressedRecordCount, actual.CompressedRecordCount);
        Assert.Equal(expected.CompressedBytes, actual.CompressedBytes);
        Assert.Equal(expected.DecompressedBytes, actual.DecompressedBytes);
        Assert.Equal(expected.StreamSchemaVersion, actual.StreamSchemaVersion);
        Assert.Equal(expected.DictionaryVersion, actual.DictionaryVersion);
        Assert.Equal(expected.SourceUniverseVersion, actual.SourceUniverseVersion);
        Assert.Equal(expected.BatchCount, actual.BatchCount);
        Assert.Equal(expected.EventCount, actual.EventCount);
        Assert.Equal(expected.PayloadBytes, actual.PayloadBytes);
        Assert.Equal(expected.PayloadValueCount, actual.PayloadValueCount);
        Assert.Equal(expected.RawValueChecksum, actual.RawValueChecksum);
    }
}
