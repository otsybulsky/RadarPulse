using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

internal sealed partial class ArchiveTwoRadarEventBatchProjector : IArchiveTwoMessageConsumer
{
    public void ResetVolume(
        string radarId,
        DateTimeOffset volumeTimestamp,
        int initialEventCapacity = DefaultInitialEventCapacity,
        int initialPayloadCapacity = DefaultInitialPayloadCapacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(radarId);

        var sameRadar = string.Equals(this.radarId, radarId, StringComparison.Ordinal);
        this.radarId = radarId;
        this.volumeTimestamp = volumeTimestamp;
        volumeTimestampUtcTicks = volumeTimestamp.UtcTicks;
        radialSequenceNumber = 0;
        currentDictionaryVersion = identityNormalizer.CurrentDictionaryVersion;
        dictionarySnapshotVersion = currentDictionaryVersion;
        batchBuilder.ResetRetainingCapacity();
        batchBuilder.EnsureCapacity(initialEventCapacity, initialPayloadCapacity);

        if (!sameRadar)
        {
            if (sourceUniverse.RadarOrdinalCount == 1 && identityNormalizer.RadarCount > 0)
            {
                identityNormalizer = new RadarStreamIdentityNormalizer(sourceUniverse);
                currentDictionaryVersion = DictionaryVersion.Initial;
                dictionarySnapshotVersion = DictionaryVersion.Initial;
            }

            radarIdUtf8 = Encoding.ASCII.GetBytes(radarId);
            identityCacheByMomentCode.Clear();
        }
    }

    /// <summary>
    /// Builds an owned batch from currently staged events and resets the batch builder.
    /// </summary>
    public RadarEventBatch BuildBatch()
    {
        var batch = batchBuilder.BuildAndReset();
        if (batch.DictionaryVersion.Value > dictionarySnapshotVersion.Value)
        {
            dictionarySnapshotVersion = batch.DictionaryVersion;
        }

        return batch;
    }

    /// <summary>
    /// Publishes a leased batch when events are staged and records the published dictionary version.
    /// </summary>
    public void PublishLeasedBatch(
        IArchiveRadarEventBatchPublisher publisher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        if (batchBuilder.EventCount == 0)
        {
            return;
        }

        var publishedDictionaryVersion = DictionaryVersion.Initial;
        batchBuilder.ConsumeLeased(batch =>
        {
            publishedDictionaryVersion = batch.DictionaryVersion;
            publisher.Publish(batch, cancellationToken);
        });

        if (publishedDictionaryVersion.Value > dictionarySnapshotVersion.Value)
        {
            dictionarySnapshotVersion = publishedDictionaryVersion;
        }
    }

}
