using RadarPulse.Application.Processing;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Processing;

public static partial class RadarPulseProductPipelineMapper
{
    /// <summary>
    /// Maps one processing batch read model into product batch vocabulary.
    /// </summary>
    public static RadarPulseProductBatch ToProductBatch(
        RadarProcessingBatchReadModel batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return new RadarPulseProductBatch(
            batch.ProviderSequence,
            batch.WasAccepted,
            batch.StreamEventCount,
            batch.PayloadBytes,
            batch.PayloadValueCount,
            batch.RawValueChecksum,
            batch.ProcessingStatus?.ToString(),
            batch.IsSuccessful,
            batch.Message,
            batch.TopologyVersion?.Value);
    }

    /// <summary>
    /// Maps a control result into product control summary vocabulary.
    /// </summary>
    public static RadarPulseProductControlSummary ToProductControlSummary(
        RadarProcessingProductionPipelineControlResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new RadarPulseProductControlSummary(
            result.RunId,
            result.Action.ToString(),
            ToProductOperatorSummary(result.OperatorSummary),
            result.CanceledOpenCount,
            result.ReleasedCanceledCount,
            result.DrainedProcessingCount,
            result.Message);
    }

    /// <summary>
    /// Maps one source output read model into product source vocabulary.
    /// </summary>
    public static RadarPulseProductSource ToProductSource(
        RadarProcessingSourceOutputReadModel source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var handlerValues = source.HandlerValues
            .Select(ToProductHandlerOutput)
            .ToArray();

        return new RadarPulseProductSource(
            new RadarPulseProductSourceIdentity(
                source.Identity.SourceId,
                source.Identity.RadarOrdinal,
                source.Identity.ElevationSlot,
                source.Identity.AzimuthBucket,
                source.Identity.RangeBand),
            source.IsActive,
            source.ProcessedEventCount,
            source.ProcessedPayloadValueCount,
            source.RawValueChecksum,
            source.LastMessageTimestampUtcTicks,
            source.ProcessingChecksum,
            Array.AsReadOnly(handlerValues));
    }
}
