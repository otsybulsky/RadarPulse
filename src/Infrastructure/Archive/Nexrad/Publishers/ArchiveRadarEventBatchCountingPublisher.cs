using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Radar event batch publisher decorator that counts batch, event, payload, and version totals.
/// </summary>
public sealed class ArchiveRadarEventBatchCountingPublisher : IArchiveRadarEventBatchPublisher
{
    private readonly IArchiveRadarEventBatchPublisher? innerPublisher;

    /// <summary>
    /// Creates a counting batch publisher without forwarding batches.
    /// </summary>
    public ArchiveRadarEventBatchCountingPublisher()
    {
    }

    /// <summary>
    /// Creates a counting batch publisher that forwards batches to an inner publisher.
    /// </summary>
    public ArchiveRadarEventBatchCountingPublisher(IArchiveRadarEventBatchPublisher innerPublisher)
    {
        this.innerPublisher = innerPublisher ?? throw new ArgumentNullException(nameof(innerPublisher));
    }

    /// <summary>
    /// Gets the number of batches observed.
    /// </summary>
    public long BatchCount { get; private set; }

    /// <summary>
    /// Gets the total stream events observed.
    /// </summary>
    public long EventCount { get; private set; }

    /// <summary>
    /// Gets the total payload bytes observed.
    /// </summary>
    public long PayloadBytes { get; private set; }

    /// <summary>
    /// Gets the total decoded payload values observed.
    /// </summary>
    public long PayloadValueCount { get; private set; }

    /// <summary>
    /// Gets the sum of raw payload values observed.
    /// </summary>
    public long RawValueChecksum { get; private set; }

    /// <summary>
    /// Gets the last observed stream schema version.
    /// </summary>
    public StreamSchemaVersion StreamSchemaVersion { get; private set; } = StreamSchemaVersion.Current;

    /// <summary>
    /// Gets the last observed dictionary version.
    /// </summary>
    public DictionaryVersion DictionaryVersion { get; private set; } = DictionaryVersion.Initial;

    /// <summary>
    /// Gets the last observed source-universe version.
    /// </summary>
    public SourceUniverseVersion SourceUniverseVersion { get; private set; } = SourceUniverseVersion.Initial;

    /// <inheritdoc />
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

    /// <summary>
    /// Builds a batch publish result from observed counts, file metadata, and the final dictionary snapshot.
    /// </summary>
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
