namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Incremental builder for radar event batches and contiguous payload storage.
/// </summary>
/// <remarks>
/// The builder enforces a single source-universe version per batch, tracks the maximum dictionary version
/// required by appended identities, and can produce copied owned batches or short-lived leased batches for
/// immediate consumption.
/// </remarks>
public sealed partial class RadarEventBatchBuilder
{
    private const int DefaultEventCapacity = 256;
    private const int DefaultPayloadCapacity = 4096;

    private readonly StreamSchemaVersion streamSchemaVersion;
    private RadarStreamEvent[] eventBuffer;
    private int eventCount;
    private byte[] payloadBuffer;
    private int payloadLength;
    private long payloadValueCount;
    private long rawValueChecksum;
    private DictionaryVersion dictionaryVersion = DictionaryVersion.Initial;
    private SourceUniverseVersion sourceUniverseVersion = SourceUniverseVersion.Initial;
    private bool hasSourceUniverseVersion;

    /// <summary>
    /// Creates a builder that writes batches using the current stream schema version.
    /// </summary>
    public RadarEventBatchBuilder(
        int initialEventCapacity = DefaultEventCapacity,
        int initialPayloadCapacity = DefaultPayloadCapacity)
        : this(StreamSchemaVersion.Current, initialEventCapacity, initialPayloadCapacity)
    {
    }

    /// <summary>
    /// Creates a builder that writes batches using an explicit stream schema version.
    /// </summary>
    public RadarEventBatchBuilder(
        StreamSchemaVersion streamSchemaVersion,
        int initialEventCapacity = DefaultEventCapacity,
        int initialPayloadCapacity = DefaultPayloadCapacity)
    {
        if (streamSchemaVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(streamSchemaVersion));
        }

        if (initialEventCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialEventCapacity));
        }

        if (initialPayloadCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialPayloadCapacity));
        }

        this.streamSchemaVersion = streamSchemaVersion;
        eventBuffer = initialEventCapacity == 0 ? [] : new RadarStreamEvent[initialEventCapacity];
        payloadBuffer = initialPayloadCapacity == 0 ? [] : new byte[initialPayloadCapacity];
    }

    /// <summary>
    /// Gets the number of events currently staged in the builder.
    /// </summary>
    public int EventCount => eventCount;

    /// <summary>
    /// Gets the number of payload bytes currently staged in the builder.
    /// </summary>
    public int PayloadLength => payloadLength;

    /// <summary>
    /// Gets the highest dictionary version required by staged stream identities.
    /// </summary>
    public DictionaryVersion DictionaryVersion => dictionaryVersion;

    /// <summary>
    /// Gets the source-universe version shared by all staged events.
    /// </summary>
    public SourceUniverseVersion SourceUniverseVersion => sourceUniverseVersion;
}
