using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingPersistentRadarEventBatchRecord
{
    public RadarProcessingPersistentRadarEventBatchRecord(
        int streamSchemaVersion,
        long dictionaryVersion,
        int sourceUniverseVersion,
        IReadOnlyList<RadarProcessingPersistentRadarStreamEventRecord> events,
        byte[] payload)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(streamSchemaVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dictionaryVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceUniverseVersion);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(payload);

        var eventCopy = new RadarProcessingPersistentRadarStreamEventRecord[events.Count];
        for (var i = 0; i < events.Count; i++)
        {
            eventCopy[i] = events[i] ?? throw new ArgumentException(
                "Persistent radar event batch records must not contain null events.",
                nameof(events));
        }

        StreamSchemaVersion = streamSchemaVersion;
        DictionaryVersion = dictionaryVersion;
        SourceUniverseVersion = sourceUniverseVersion;
        Events = eventCopy;
        Payload = payload.ToArray();
    }

    public int StreamSchemaVersion { get; }

    public long DictionaryVersion { get; }

    public int SourceUniverseVersion { get; }

    public RadarProcessingPersistentRadarStreamEventRecord[] Events { get; }

    public byte[] Payload { get; }

    public int StreamEventCount => Events.Length;

    public int PayloadBytes => Payload.Length;

    public RadarEventBatch ToBatch()
    {
        var events = new RadarStreamEvent[Events.Length];
        for (var i = 0; i < events.Length; i++)
        {
            events[i] = Events[i].ToEvent();
        }

        return new RadarEventBatch(
            new StreamSchemaVersion(StreamSchemaVersion),
            new DictionaryVersion(DictionaryVersion),
            new SourceUniverseVersion(SourceUniverseVersion),
            events,
            Payload.ToArray());
    }

    public static RadarProcessingPersistentRadarEventBatchRecord From(
        RadarEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var events = new RadarProcessingPersistentRadarStreamEventRecord[batch.Events.Length];
        var span = batch.Events.Span;
        for (var i = 0; i < span.Length; i++)
        {
            events[i] = RadarProcessingPersistentRadarStreamEventRecord.From(span[i]);
        }

        return new RadarProcessingPersistentRadarEventBatchRecord(
            batch.StreamSchemaVersion.Value,
            batch.DictionaryVersion.Value,
            batch.SourceUniverseVersion.Value,
            events,
            batch.Payload.Span.ToArray());
    }
}
