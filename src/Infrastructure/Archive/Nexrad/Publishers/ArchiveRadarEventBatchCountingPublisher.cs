using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

public sealed class ArchiveRadarEventBatchCountingPublisher : IArchiveRadarEventBatchPublisher
{
    private readonly IArchiveRadarEventBatchPublisher? innerPublisher;

    public ArchiveRadarEventBatchCountingPublisher()
    {
    }

    public ArchiveRadarEventBatchCountingPublisher(IArchiveRadarEventBatchPublisher innerPublisher)
    {
        this.innerPublisher = innerPublisher ?? throw new ArgumentNullException(nameof(innerPublisher));
    }

    public long BatchCount { get; private set; }

    public long EventCount { get; private set; }

    public long PayloadBytes { get; private set; }

    public long PayloadValueCount { get; private set; }

    public long RawValueChecksum { get; private set; }

    public StreamSchemaVersion StreamSchemaVersion { get; private set; } = StreamSchemaVersion.Current;

    public DictionaryVersion DictionaryVersion { get; private set; } = DictionaryVersion.Initial;

    public SourceUniverseVersion SourceUniverseVersion { get; private set; } = SourceUniverseVersion.Initial;

    public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        innerPublisher?.Publish(batch, cancellationToken);

        BatchCount++;
        EventCount += batch.EventCount;
        PayloadBytes += batch.PayloadLength;
        if (batch.TryGetPayloadMetrics(out var payloadValueCount, out var rawValueChecksum))
        {
            PayloadValueCount += payloadValueCount;
            RawValueChecksum += rawValueChecksum;
        }
        else
        {
            var metrics = RadarEventBatchMetrics.Compute(batch);
            PayloadValueCount += metrics.PayloadValueCount;
            RawValueChecksum += metrics.RawValueChecksum;
        }

        StreamSchemaVersion = batch.StreamSchemaVersion;
        DictionaryVersion = batch.DictionaryVersion;
        SourceUniverseVersion = batch.SourceUniverseVersion;
    }

    public ArchiveRadarEventBatchPublishResult BuildResult(
        string filePath,
        string decompressor,
        int degreeOfParallelism,
        long fileSizeBytes,
        int compressedRecordCount,
        long compressedBytes,
        long decompressedBytes,
        RadarStreamDictionarySnapshot dictionarySnapshot) =>
        new(
            filePath,
            decompressor,
            degreeOfParallelism,
            fileSizeBytes,
            compressedRecordCount,
            compressedBytes,
            decompressedBytes,
            StreamSchemaVersion,
            DictionaryVersion,
            SourceUniverseVersion,
            BatchCount,
            EventCount,
            PayloadBytes,
            PayloadValueCount,
            RawValueChecksum,
            dictionarySnapshot);
}
