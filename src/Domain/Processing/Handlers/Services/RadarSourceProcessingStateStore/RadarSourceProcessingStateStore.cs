using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Source-local processing state and handler state store for one processing core.
/// </summary>
/// <remarks>
/// The store tracks source activity, processing counts, checksums, source-local
/// ordering, and optional handler slots. Handler state is stored per source using
/// the slot layout built from configured handlers.
/// </remarks>
public sealed partial class RadarSourceProcessingStateStore
{
    private readonly RadarSourceProcessingHandlerSlotLayout handlerSlotLayout;
    private readonly bool[] activeSources;
    private readonly long[] processedEventCounts;
    private readonly long[] processedPayloadValueCounts;
    private readonly long[] rawValueChecksums;
    private readonly long[] lastMessageTimestampUtcTicks;
    private readonly ulong[] processingChecksums;
    private readonly long[] handlerInt64Slots;
    private readonly double[] handlerDoubleSlots;
    private long activeSourceCount;

    /// <summary>
    /// Creates state store for a source universe and optional source handlers.
    /// </summary>
    public RadarSourceProcessingStateStore(
        RadarSourceUniverse sourceUniverse,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null)
        : this(
            sourceUniverse,
            new RadarSourceProcessingHandlerSlotLayout(handlers))
    {
    }

    internal RadarSourceProcessingStateStore(
        RadarSourceUniverse sourceUniverse,
        RadarSourceProcessingHandlerSlotLayout handlerSlotLayout)
    {
        ArgumentNullException.ThrowIfNull(sourceUniverse);
        ArgumentNullException.ThrowIfNull(handlerSlotLayout);

        SourceUniverseVersion = sourceUniverse.Version;
        SourceCount = sourceUniverse.SourceCount;
        this.handlerSlotLayout = handlerSlotLayout;

        activeSources = new bool[SourceCount];
        processedEventCounts = new long[SourceCount];
        processedPayloadValueCounts = new long[SourceCount];
        rawValueChecksums = new long[SourceCount];
        lastMessageTimestampUtcTicks = new long[SourceCount];
        processingChecksums = new ulong[SourceCount];
        handlerInt64Slots = CreateInt64HandlerSlots(SourceCount, handlerSlotLayout.TotalInt64SlotCount);
        handlerDoubleSlots = CreateDoubleHandlerSlots(SourceCount, handlerSlotLayout.TotalDoubleSlotCount);
    }

    /// <summary>
    /// Source universe version backing the state store.
    /// </summary>
    public SourceUniverseVersion SourceUniverseVersion { get; }

    /// <summary>
    /// Number of sources tracked by the store.
    /// </summary>
    public int SourceCount { get; }

    /// <summary>
    /// Number of sources that have received at least one event.
    /// </summary>
    public long ActiveSourceCount => Volatile.Read(ref activeSourceCount);

    /// <summary>
    /// Handler slot layout used by the store.
    /// </summary>
    public RadarSourceProcessingHandlerSlotLayout HandlerSlotLayout => handlerSlotLayout;
}
