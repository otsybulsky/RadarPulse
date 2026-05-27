namespace RadarPulse.Application.Processing;

/// <summary>
/// BFF-facing processing output for one source.
/// </summary>
public sealed class RadarProcessingSourceOutputReadModel
{
    private readonly IReadOnlyList<RadarProcessingHandlerOutputValueReadModel> handlerValues;

    /// <summary>
    /// Creates a source output read model with optional handler values.
    /// </summary>
    public RadarProcessingSourceOutputReadModel(
        RadarProcessingSourceIdentityReadModel identity,
        bool isActive,
        long processedEventCount,
        long processedPayloadValueCount,
        long rawValueChecksum,
        long lastMessageTimestampUtcTicks,
        ulong processingChecksum,
        IReadOnlyList<RadarProcessingHandlerOutputValueReadModel>? handlerValues = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegative(processedEventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(processedPayloadValueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(rawValueChecksum);
        ArgumentOutOfRangeException.ThrowIfNegative(lastMessageTimestampUtcTicks);

        Identity = identity;
        IsActive = isActive;
        ProcessedEventCount = processedEventCount;
        ProcessedPayloadValueCount = processedPayloadValueCount;
        RawValueChecksum = rawValueChecksum;
        LastMessageTimestampUtcTicks = lastMessageTimestampUtcTicks;
        ProcessingChecksum = processingChecksum;
        this.handlerValues = CopyValues(handlerValues);
    }

    /// <summary>
    /// Source identity.
    /// </summary>
    public RadarProcessingSourceIdentityReadModel Identity { get; }

    /// <summary>
    /// Dense source id.
    /// </summary>
    public int SourceId => Identity.SourceId;

    /// <summary>
    /// Indicates whether the source received any processed event.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Number of processed events for the source.
    /// </summary>
    public long ProcessedEventCount { get; }

    /// <summary>
    /// Processed payload value count for the source.
    /// </summary>
    public long ProcessedPayloadValueCount { get; }

    /// <summary>
    /// Raw payload checksum for the source.
    /// </summary>
    public long RawValueChecksum { get; }

    /// <summary>
    /// Last message timestamp observed for the source.
    /// </summary>
    public long LastMessageTimestampUtcTicks { get; }

    /// <summary>
    /// Deterministic processing checksum for the source.
    /// </summary>
    public ulong ProcessingChecksum { get; }

    /// <summary>
    /// Exported handler values for the source.
    /// </summary>
    public IReadOnlyList<RadarProcessingHandlerOutputValueReadModel> HandlerValues => handlerValues;

    private static IReadOnlyList<RadarProcessingHandlerOutputValueReadModel> CopyValues(
        IReadOnlyList<RadarProcessingHandlerOutputValueReadModel>? values)
    {
        if (values is null || values.Count == 0)
        {
            return Array.Empty<RadarProcessingHandlerOutputValueReadModel>();
        }

        var result = new RadarProcessingHandlerOutputValueReadModel[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            result[i] = values[i] ?? throw new ArgumentNullException(nameof(values));
        }

        return Array.AsReadOnly(result);
    }
}
