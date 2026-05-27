using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Persistable representation of a radar event batch and its shared payload buffer.
/// </summary>
/// <remarks>
/// The constructor defensively copies events and payload bytes so durable store
/// persistence is isolated from mutable caller arrays.
/// </remarks>
public sealed class RadarProcessingPersistentRadarEventBatchRecord
{
    /// <summary>
    /// Creates a persistent batch record from version fields, event records, and payload bytes.
    /// </summary>
    public RadarProcessingPersistentRadarEventBatchRecord(
        int streamSchemaVersion,
        long dictionaryVersion,
        int sourceUniverseVersion,
        RadarProcessingPersistentRadarStreamEventRecord[] events,
        byte[] payload)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(streamSchemaVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dictionaryVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceUniverseVersion);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(payload);

        var eventCopy = new RadarProcessingPersistentRadarStreamEventRecord[events.Length];
        for (var i = 0; i < events.Length; i++)
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

    /// <summary>
    /// Stream schema version value for the serialized batch.
    /// </summary>
    public int StreamSchemaVersion { get; }

    /// <summary>
    /// Dense identity dictionary version value for the serialized batch.
    /// </summary>
    public long DictionaryVersion { get; }

    /// <summary>
    /// Source universe version value for the serialized batch.
    /// </summary>
    public int SourceUniverseVersion { get; }

    /// <summary>
    /// Serialized stream events that reference offsets in <see cref="Payload"/>.
    /// </summary>
    public RadarProcessingPersistentRadarStreamEventRecord[] Events { get; }

    /// <summary>
    /// Serialized batch payload buffer.
    /// </summary>
    public byte[] Payload { get; }

    /// <summary>
    /// Number of stream events in the batch.
    /// </summary>
    public int StreamEventCount => Events.Length;

    /// <summary>
    /// Number of bytes retained by the serialized payload buffer.
    /// </summary>
    public int PayloadBytes => Payload.Length;

    /// <summary>
    /// Rehydrates the domain batch represented by this persistent record.
    /// </summary>
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

    /// <summary>
    /// Creates a persistent record from a domain radar event batch.
    /// </summary>
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
