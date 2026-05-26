namespace RadarPulse.Application.Processing;

public sealed class RadarProcessingSourceOutputReadModel
{
    private readonly IReadOnlyList<RadarProcessingHandlerOutputValueReadModel> handlerValues;

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

    public RadarProcessingSourceIdentityReadModel Identity { get; }

    public int SourceId => Identity.SourceId;

    public bool IsActive { get; }

    public long ProcessedEventCount { get; }

    public long ProcessedPayloadValueCount { get; }

    public long RawValueChecksum { get; }

    public long LastMessageTimestampUtcTicks { get; }

    public ulong ProcessingChecksum { get; }

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

